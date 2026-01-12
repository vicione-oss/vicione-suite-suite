using System.IO.Abstractions;
using System.Net.Mime;
using System.Runtime.InteropServices;
using Core.Module.Contracts;
using Core.Module.Extensions;
using Sdk.Backend.ArtifactApi;
using Sdk.Modules;

namespace Core.Module;

public sealed class ModuleApiAdapter(IArtifactQueryApi artifactApi, IFileSystem fileSystem) : IModuleApiAdapter
{
    private const string ModulesBaseFolder = "modules"; // Base folder in the repository

    public async Task<ModulePackageDownloadResult[]> DownloadAndExtract(string modulesPath, IEnumerable<ModuleDependencyPackage> packages, CancellationToken cancellationToken)
        => await Task.WhenAll(packages.Select(mp => DownloadAndExtract(modulesPath, mp, cancellationToken)));

    public async Task<ModulePackageDownloadResult> DownloadAndExtract(string modulesPath, ModuleDependencyPackage package, CancellationToken cancellationToken)
    {
        var targetPath = Path.Combine(
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

            // download the module zip and extract it
            var moduleArtifact = CreateModuleArtifactInfo(package);
            await artifactApi.DownloadAndExtract(moduleArtifact, targetPath, cancellationToken);

            // because the metadata is not part of the zip we try download it and put it into module directory.            
            // we don't know with which sdk version the module got published
            // we have to query a matching one by package version                       
            var metadataPath = fileSystem.Path.Combine(targetPath, package.GetLocalMetadataFileName());
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
        var aqlQuery = artifactApi.CreateQueryBuilder()
            .AndPathMatches($"{ModulesBaseFolder}/{package.Name}")
            .AndNameMatches($"*{package.Version}-{RuntimeInformation.RuntimeIdentifier}_*.json")
            .OrderByDescending("path", "name")
            .BuildQueryString();

        var result = await artifactApi.ExecuteQuery(aqlQuery, cancellationToken);
        var artifactoryAsset = result.Artifacts.FirstOrDefault()
            ?? throw new InvalidOperationException($"No metadata asset found for module '{package.Name}' v{package.Version}");

        return await artifactApi.DownloadStream(artifactoryAsset, cancellationToken);
    }

    public async Task<ModuleMetadata?> GetModuleMetadata(ModuleArtifactInfo artifactInfo, CancellationToken cancellationToken = default)
    {
        var metadataStream = await artifactApi.DownloadStream(artifactInfo, cancellationToken);
        return await System.Text.Json.JsonSerializer.DeserializeAsync<ModuleMetadata?>(metadataStream, ModuleSerializerOptions.GetOptions(), cancellationToken);
    }

    public async Task<List<ModuleArtifactInfo>> QueryModuleAssets(CancellationToken cancellationToken = default)
    {
        var aqlQuery = artifactApi.CreateQueryBuilder()
            .AndPathMatches($"{ModulesBaseFolder}/*")
            .AndNameMatches($"*{RuntimeInformation.RuntimeIdentifier}.zip")
            .OrderByDescending("path", "name")
            .BuildQueryString();

        var result = await artifactApi.ExecuteQuery(aqlQuery, cancellationToken);

        return [.. result.Artifacts.Select(MapArtifactoryItemToGenericInfo)];
    }

    public async Task<List<ModuleArtifactInfo>> QueryModuleMetadataArtifacts(Version? sdkVersion = null,
        bool includePreRelease = true,
        CancellationToken cancellationToken = default)
    {
        var queryBuilder = artifactApi.CreateQueryBuilder()
            .AndPathMatches($"{ModulesBaseFolder}/*")
            .AndNameMatches($"{CreateNameMatchFilter(sdkVersion)}.json")
            .OrderByDescending("path", "name");

        // 0.28.0-ci1523472-linux-arm64_0.25.0.json
        if (!includePreRelease)
            queryBuilder.AndNameNotMatches("*-ci*");

        var aqlQuery = queryBuilder.BuildQueryString();
        var result = await artifactApi.ExecuteQuery(aqlQuery, cancellationToken);

        return [.. result.Artifacts.Select(MapArtifactoryItemToGenericInfo)];
    }

    public async Task<ModuleArtifactInfo?> QueryLatestModuleMetadataArtifact(Version sdkVersion,
        string packageName,
        bool includePreRelease = false,
        CancellationToken cancellationToken = default)
    {
        var queryBuilder = artifactApi.CreateQueryBuilder()
            .AndPathMatches($"{ModulesBaseFolder}/{packageName}")
            .AndNameMatches($"{CreateNameMatchFilter(sdkVersion)}.json")
            .OrderByDescending("name");

        // e.g. 0.28.0-ci1523472-linux-arm64_0.25.0.json
        if (!includePreRelease)
            queryBuilder.AndNameNotMatches("*-ci*");

        var aqlQuery = queryBuilder.BuildQueryString();
        var result = await artifactApi.ExecuteQuery(aqlQuery, cancellationToken);

        // Map, Filter in C#, Find Latest
        return result.Artifacts.Select(MapArtifactoryItemToGenericInfo)
            .FirstOrDefault(); // AQL already sorted, first item after filtering is latest
    }

    private static string CreateNameMatchFilter(Version? sdkVersion)
    {
        if (sdkVersion is null)
            return $"*{RuntimeInformation.RuntimeIdentifier}*";

        return $"*{RuntimeInformation.RuntimeIdentifier}*_{sdkVersion.Major}.{sdkVersion.Minor}.*";
    }

    private IArtifactItem CreateModuleArtifactInfo(ModuleDependencyPackage package)
    {
        var runtimeIdentifier = RuntimeInformation.RuntimeIdentifier;
        var name = $"{package.Version}-{runtimeIdentifier}.zip";
        var path = $"{ModulesBaseFolder}/{package.Name}";

        // e.g. name -> 0.7.0-win-x64.zip
        //      path -> modules/ViciOne.Suite.Ping 
        return artifactApi.CreateArtifactItem(path, name);
    }

    private ModuleArtifactInfo MapArtifactoryItemToGenericInfo(IArtifactItem item)
    {
        return new ModuleArtifactInfo
        {
            Name = item.Name,
            Path = item.Path,
            Repo = item.Repo,
            Size = item.Size,
            Modified = item.Modified ?? DateTime.MinValue, // Ensure DateTimeOffset
            Checksum = item.Checksum,
            ContentType = GuessContentTypeFromName(item.Name)
        };
    }

    private static string? GuessContentTypeFromName(string name)
    {
        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return MediaTypeNames.Application.Zip;

        if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return MediaTypeNames.Application.Json;

        return MediaTypeNames.Application.Octet; // Default binary type
    }

    private bool ModuleVersionExists(string modulePath)
        => fileSystem.Directory.Exists(modulePath) && fileSystem.Directory.GetFiles(modulePath).Length > 0;
}
