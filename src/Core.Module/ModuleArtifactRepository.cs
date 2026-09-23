using System.IO.Abstractions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Core.Module.Contracts;
using Core.Module.Extensions;
using Core.Module.Utils;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Artifacts;
using Sdk.Modules;
using Semver;

namespace Core.Module;

public sealed partial class ModuleArtifactRepository(IArtifactRepository artifactRepository, IFileSystem fileSystem, ILogger<ModuleArtifactRepository> logger) : IModuleArtifactRepository
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
                // Cancellation needs no handling.
            }
            catch (ObjectDisposedException)
            {
                // The semaphore is already disposed; nothing left to release.
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

            // Sweep orphaned staging directories left behind by a previously interrupted run
            // (e.g. a killed debug session). A still-locked one is skipped best-effort and retried later.
            CleanupOrphanedStagingDirectories(packageFolder);

            fileSystem.Directory.CreateDirectory(stagingPath);

            var moduleArtifact = await QueryModuleArtifact(package, cancellationToken)
                ?? throw new InvalidOperationException($"Can't find artifact for package='{package.Name}' version='{package.Version}'");

            await artifactRepository.DownloadAndExtract(moduleArtifact, stagingPath, cancellationToken);

            // The metadata is not part of the zip and the publishing sdk version is unknown, so it is
            // downloaded separately by querying for a match on the package version.
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

    private void CleanupOrphanedStagingDirectories(string packageFolder)
    {
        if (!fileSystem.Directory.Exists(packageFolder))
            return;

        try
        {
            foreach (var directory in fileSystem.Directory.EnumerateDirectories(packageFolder, ".staging-*"))
                TryDeleteDirectory(directory);
        }
        catch (IOException)
        {
            // Best-effort sweep; a locked or missing folder is retried on a later run.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort sweep; a locked or missing folder is retried on a later run.
        }
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
        // Finds the metadata asset by AQL:
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

        LogAqlQuery(logger, nameof(QueryModuleArtifacts), aqlQuery);

        var result = await artifactRepository.Query(aqlQuery, cancellationToken);

        LogQueryResultErrors(result);

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

        LogAqlQuery(logger, nameof(QueryModuleMetadataArtifacts), aqlQuery);

        var result = await artifactRepository.Query(aqlQuery, cancellationToken);

        LogQueryResultErrors(result);

        // The name filter has already matched the major version.
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

        // e.g. 0.28.0-ci1523472-linux-arm64_0.25.0.json
        var aqlQuery = queryBuilder.Build();

        LogAqlQuery(logger, nameof(QueryModuleArtifact), aqlQuery);

        var result = await artifactRepository.Query(aqlQuery, cancellationToken);

        LogQueryResultErrors(result);

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

        LogAqlQuery(logger, nameof(QueryLatestModuleMetadataArtifact), aqlQuery);

        var result = await artifactRepository.Query(aqlQuery, cancellationToken);

        LogQueryResultErrors(result);

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

        // Limited to major.minor; any patch version is accepted.
        return $"*{RuntimeInformation.RuntimeIdentifier}*_{sdkVersion.Major}.*.json";
    }

    private bool ModuleVersionExists(string modulePath)
        => fileSystem.File.Exists(fileSystem.Path.Combine(modulePath, ModuleHelpers.CompletenessMarkerFileName));


    private void LogQueryResultErrors(IArtifactQueryResult result, [CallerMemberName] string? caller = "")
    {
        LogQueryResult(logger, caller, result.Artifacts.Count, result.Errors?.Count);

        if (result.Errors?.Count > 0)
        {
            foreach (var error in result.Errors)
            {
                LogQueryResultError(logger, error.Source, error.Error.Message);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Aql '{Name}': {Query}")]
    private static partial void LogAqlQuery(ILogger<ModuleArtifactRepository> logger, string name, string query);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Query='{Caller}' results: {Count}, errors: {Errors}")]
    private static partial void LogQueryResult(ILogger<ModuleArtifactRepository> logger, string? caller, int count, int? errors);

    [LoggerMessage(Level = LogLevel.Error, Message = "Source='{Source}' result error: {Error}")]
    private static partial void LogQueryResultError(ILogger<ModuleArtifactRepository> logger, string source, string? error);
}
