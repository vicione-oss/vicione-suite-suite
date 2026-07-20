using System.IO.Abstractions;
using System.Runtime.InteropServices;
using Core.Module.Contracts;
using Core.Module.Extensions;
using Core.Module.Utils;
using Sdk.Backend.Artifacts;
using Sdk.Modules;
using Semver;

namespace Core.Module;

public sealed class ModuleArtifactRepository(IArtifactRepository artifactRepository, IFileSystem fileSystem) : IModuleArtifactRepository
{
    private const string ModulesBaseFolder = "modules"; // Base folder in the repository
    private const int MaxConcurrentDownloads = 5;

    public async Task<ModulePackageDownloadResult[]> DownloadAndExtract(string modulesPath, IEnumerable<ModuleDependencyPackage> packages, CancellationToken cancellationToken)
    {
        var packageList = packages as IReadOnlyList<ModuleDependencyPackage> ?? [.. packages];
        if (packageList.Count == 0)
            return [];

        var results = new ModulePackageDownloadResult[packageList.Count];

        // Use a bounded sliding window instead of an unbounded Task.WhenAll to cap concurrency.
        using var throttle = new SemaphoreSlim(MaxConcurrentDownloads, MaxConcurrentDownloads);
        var tasks = packageList.Select((package, index) => ThrottledDownloadAndExtract(index, package, throttle)).ToArray();
        await Task.WhenAll(tasks);

        return results;

        async Task ThrottledDownloadAndExtract(int index, ModuleDependencyPackage package, SemaphoreSlim semaphore)
        {
            try
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    results[index] = await DownloadAndExtract(modulesPath, package, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                // Nothing to do here, we return gracefully
            }
            catch (ObjectDisposedException)
            {
                // Semaphore or other object already disposed, nothing we can do, return gracefully
            }
        }
    }

    public async Task<ModulePackageDownloadResult> DownloadAndExtract(string modulesPath, ModuleDependencyPackage package, CancellationToken cancellationToken)
    {
        var targetPath = fileSystem.Path.Combine(
        [
            modulesPath,
            package.Name,
            package.Version,
        ]);

        var result = new ModulePackageDownloadResult(package);

        // Stage into a sibling directory on the same volume so promotion is an atomic rename.
        var packageFolder = fileSystem.Path.Combine(modulesPath, package.Name);
        var stagingPath = fileSystem.Path.Combine(packageFolder, $".staging-{Guid.NewGuid():N}");

        try
        {
            // A fully installed module is marked complete; anything else is treated as not-installed.
            if (ModuleVersionExists(targetPath))
            {
                result.Skipped = true;
                return result;
            }

            // A leftover directory without the completeness marker is partial/corrupt; discard it.
            if (fileSystem.Directory.Exists(targetPath))
                fileSystem.Directory.Delete(targetPath, recursive: true);

            fileSystem.Directory.CreateDirectory(stagingPath);

            // try to find the matching artifact on the repository                      
            var moduleArtifact = await QueryModuleArtifact(package, cancellationToken)
                ?? throw new InvalidOperationException($"Can't find artifact for package='{package.Name}' version='{package.Version}'");

            // download the module zip and extract it into the staging directory
            await artifactRepository.DownloadAndExtract(moduleArtifact, stagingPath, cancellationToken);

            // because the metadata is not part of the zip we try download it and put it into module directory.            
            // we don't know with which sdk version the module got published
            // we have to query a matching one by package version                       
            var metadataPath = fileSystem.Path.Combine(stagingPath, ModuleHelpers.GetLocalMetadataFileName(package.Name));
            await using (var metadataDownloadStream = await GetMetadataDownloadStream(package, cancellationToken))
            await using (var metadataFileStream = fileSystem.FileStream.New(metadataPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
            {
                await metadataDownloadStream.CopyToAsync(metadataFileStream, cancellationToken);
            }

            // Verify the staged content is complete before it is made visible under its version folder.
            VerifyStagedModule(stagingPath, metadataPath);

            // Write the completeness marker as the very last write into staging.
            var markerPath = fileSystem.Path.Combine(stagingPath, ModuleHelpers.CompletenessMarkerFileName);
            await fileSystem.File.WriteAllTextAsync(markerPath, DateTimeOffset.UtcNow.ToString("O"), cancellationToken);

            // Atomically promote the fully-staged module to its final versioned folder.
            fileSystem.Directory.CreateDirectory(packageFolder);
            fileSystem.Directory.Move(stagingPath, targetPath);
        }
        catch (Exception e)
        {
            result.Error = e;
        }
        finally
        {
            // Always clean up staging: on success it has been moved away, on failure it must not linger.
            TryDeleteDirectory(stagingPath);
        }

        return result;
    }

    private void VerifyStagedModule(string stagingPath, string metadataPath)
    {
        if (!fileSystem.File.Exists(metadataPath))
            throw new InvalidOperationException($"Staged module is incomplete: metadata file missing at '{metadataPath}'.");

        // The extracted archive must have produced at least one payload file besides the metadata.
        var hasPayload = fileSystem.Directory
            .EnumerateFiles(stagingPath, "*", SearchOption.AllDirectories)
            .Any(file => !string.Equals(file, metadataPath, StringComparison.Ordinal));

        if (!hasPayload)
            throw new InvalidOperationException($"Staged module is incomplete: no extracted payload found in '{stagingPath}'.");
    }

    private void TryDeleteDirectory(string path)
    {
        try
        {
            if (fileSystem.Directory.Exists(path))
                fileSystem.Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup; leftover staging directories are ignored on next run (no marker).
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup; leftover staging directories are ignored on next run (no marker).
        }
    }

    public async Task<ModuleMetadata?> GetModuleMetadata(ModuleDependencyPackage package, CancellationToken cancellationToken = default)
    {
        var metadataStream = await GetMetadataDownloadStream(package, cancellationToken);
        return await System.Text.Json.JsonSerializer.DeserializeAsync<ModuleMetadata?>(metadataStream, ModuleSerializerOptions.GetOptions(), cancellationToken);
    }

    public async Task<Stream> GetMetadataDownloadStream(ModuleDependencyPackage package,
        CancellationToken cancellationToken = default)
    {
        // Find the specific metadata asset using AQL
        // 0.33.0-ci1632286-win-x64_0.28.0.json
        // -> .../vicione-suite/modules/ViciOne.Suite.ClusterManagement/0.32.1-linux-arm64.zip
        var aqlQuery = artifactRepository.CreateQueryBuilder()
            .AndPathMatches($"{ModulesBaseFolder}/{package.Name}")
            .AndNameMatches($"*{package.Version}-{RuntimeInformation.RuntimeIdentifier}_*.json")
            .OrderByDescending("path", "name")
            .Build();

        var result = await artifactRepository.Query(aqlQuery, cancellationToken);
        var artifactoryAsset = result.Artifacts.FirstOrDefault()
            ?? throw new InvalidOperationException($"No metadata asset found for module '{package.Name}' v{package.Version}");

        return await artifactRepository.Download(artifactoryAsset, cancellationToken);
    }

    public async Task<ModuleMetadata?> GetModuleMetadata(IArtifact metadataArtifact, CancellationToken cancellationToken = default)
    {
        var metadataStream = await artifactRepository.Download(metadataArtifact, cancellationToken);
        return await System.Text.Json.JsonSerializer.DeserializeAsync<ModuleMetadata?>(metadataStream, ModuleSerializerOptions.GetOptions(), cancellationToken);
    }

    public async Task<List<IArtifact>> QueryModuleArtifacts(CancellationToken cancellationToken = default)
    {
        var aqlQuery = artifactRepository.CreateQueryBuilder()
            .AndPathMatches($"{ModulesBaseFolder}/*")
            .AndNameMatches($"*{RuntimeInformation.RuntimeIdentifier}.zip")
            .OrderByDescending("path", "name")
            .Build();

        var result = await artifactRepository.Query(aqlQuery, cancellationToken);

        return [.. result.Artifacts];
    }

    public async Task<List<IArtifact>> QueryModuleMetadataArtifacts(Version? sdkVersion = null, DateTimeOffset? modifiedAfter = null,
        CancellationToken cancellationToken = default)
    {
        var queryBuilder = artifactRepository.CreateQueryBuilder()
            .AndPathMatches($"{ModulesBaseFolder}/*")
            .AndNameMatches(GetMetadataNameFilter(sdkVersion))
            .OrderByDescending("path", "name");

        if (modifiedAfter is not null)
            queryBuilder.ModifiedAfter(modifiedAfter.Value);

        // e.g. items.find({"modified":{"$gt":"2026-04-13T03:59:57.57TZD"}},{"repo":"__repository__","$and":[{"path":{"$match":"modules/*"}},{"name":{"$match":"*win-x64*.json"}}]}).sort({"$desc":["path","name"]})
        var aqlQuery = queryBuilder.Build();
        var result = await artifactRepository.Query(aqlQuery, cancellationToken);

        // our major version matches already by name filter
        if (sdkVersion is null)
            return [.. result.Artifacts];

        var sdkSemVersion = SemVersion.FromVersion(sdkVersion);

        return [.. result.FilterCompatibleModuleArtifactsBySdkVersion(sdkSemVersion)];
    }

    public async Task<IArtifact?> QueryModuleArtifact(ModuleDependencyPackage package,
        CancellationToken cancellationToken = default)
    {
        var queryBuilder = artifactRepository.CreateQueryBuilder()
            .AndPathMatches($"{ModulesBaseFolder}/{package.Name}")
            .AndNameMatches($"{package.Version}-{RuntimeInformation.RuntimeIdentifier}.zip");

        // 0.28.0-ci1523472-linux-arm64_0.25.0.json        
        var aqlQuery = queryBuilder.Build();
        var result = await artifactRepository.Query(aqlQuery, cancellationToken);

        return result.Artifacts.FirstOrDefault();
    }

    public async Task<IArtifact?> QueryLatestModuleMetadataArtifact(Version sdkVersion,
        string packageName, int? major = null, int? minor = null,
        CancellationToken cancellationToken = default)
    {
        if (minor != null && major is null)
            throw new InvalidOperationException("Invalid filter combination: minor cannot be set without major.");

        var queryBuilder = artifactRepository.CreateQueryBuilder()
            .AndPathMatches($"{ModulesBaseFolder}/{packageName}")
            .AndNameMatches(GetMetadataNameFilter(sdkVersion))
            .OrderByDescending("name");

        var aqlQuery = queryBuilder.Build();
        var result = await artifactRepository.Query(aqlQuery, cancellationToken);

        return result.OrderModuleArtifactsByVersionDesc(k => !k.IsPrerelease &&
            (major is null || major == k.Major) &&
            (minor is null || minor == k.Minor))
            .FirstOrDefault();
    }

    public IReadOnlyCollection<string> GetSourceKeys()
        => [.. artifactRepository.GetSourceKeys()];

    private static string GetMetadataNameFilter(Version? sdkVersion)
    {
        if (sdkVersion is null)
            return $"*{RuntimeInformation.RuntimeIdentifier}*.json";

        // We limit to major.minor version and allow different patch versions
        return $"*{RuntimeInformation.RuntimeIdentifier}*_{sdkVersion.Major}.*.json";
    }

    private bool ModuleVersionExists(string modulePath)
        => fileSystem.File.Exists(fileSystem.Path.Combine(modulePath, ModuleHelpers.CompletenessMarkerFileName));
}
