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

    public async Task<ModulePackageDownloadResult[]> DownloadAndExtract(string modulesPath, IEnumerable<ModuleDependencyPackage> packages, CancellationToken cancellationToken)
        => await Task.WhenAll(packages.Select(mp => DownloadAndExtract(modulesPath, mp, cancellationToken)));

    public async Task<ModulePackageDownloadResult> DownloadAndExtract(string modulesPath, ModuleDependencyPackage package, CancellationToken cancellationToken)
    {
        var targetPath = fileSystem.Path.Combine(
        [
            modulesPath,
            package.Name,
            package.Version,
        ]);

        var result = new ModulePackageDownloadResult(package);

        try
        {
            // target path e.g. /path/to/modules/name/version
            if (ModuleVersionExists(targetPath))
            {
                result.Skipped = true;
                return result;
            }

            // try to find the matching artifact on the repository                      
            var moduleArtifact = await QueryModuleArtifact(package, cancellationToken)
                ?? throw new InvalidOperationException($"Can't find artifact for package='{package.Name}' version='{package.Version}'");

            // download the module zip and extract it
            await artifactRepository.DownloadAndExtract(moduleArtifact, targetPath, cancellationToken);

            // because the metadata is not part of the zip we try download it and put it into module directory.            
            // we don't know with which sdk version the module got published
            // we have to query a matching one by package version                       
            var metadataPath = fileSystem.Path.Combine(targetPath, ModuleHelpers.GetLocalMetadataFileName(package.Name));
            await using var metadataDownloadStream = await GetMetadataDownloadStream(package, cancellationToken);
            await using var metadataFileStream = fileSystem.FileStream.New(metadataPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);

            await metadataDownloadStream.CopyToAsync(metadataFileStream, cancellationToken);
        }
        catch (Exception e)
        {
            result.Error = e;
        }

        return result;
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
        string packageName,
        CancellationToken cancellationToken = default)
    {
        var queryBuilder = artifactRepository.CreateQueryBuilder()
            .AndPathMatches($"{ModulesBaseFolder}/{packageName}")
            .AndNameMatches(GetMetadataNameFilter(sdkVersion))
            .OrderByDescending("name");

        var aqlQuery = queryBuilder.Build();
        var result = await artifactRepository.Query(aqlQuery, cancellationToken);

        return result.OrderModuleArtifactsByVersionDesc(k => !k.IsPrerelease)
            .FirstOrDefault();
    }

    private static string GetMetadataNameFilter(Version? sdkVersion)
    {
        if (sdkVersion is null)
            return $"*{RuntimeInformation.RuntimeIdentifier}*.json";

        // We limit to major.minor version and allow different patch versions
        return $"*{RuntimeInformation.RuntimeIdentifier}*_{sdkVersion.Major}.*.json";
    }

    private bool ModuleVersionExists(string modulePath)
        => fileSystem.Directory.Exists(modulePath) && fileSystem.Directory.GetFiles(modulePath).Length > 0;
}
