using Core.Module.Contracts;
using Sdk.Modules;

namespace Core.Module;

public interface IModuleApiAdapter
{
    Task<ModulePackageDownloadResult[]> DownloadAndExtract(string modulesPath, IEnumerable<ModuleDependencyPackage> packages, CancellationToken cancellationToken);

    /// <summary>
    /// Downloads module zip and its metadata.json matching to package name and version. The zip gets directly
    /// extracted to modules path into subfolder /{PackageName}/{PackageVersion}
    /// </summary>
    Task<ModulePackageDownloadResult> DownloadAndExtract(string modulesPath, ModuleDependencyPackage package, CancellationToken cancellationToken);

    /// <summary>
    /// Returns stream to module metadata json matching to package name and version
    /// </summary>
    Task<Stream> GetMetadataDownloadStream(ModuleDependencyPackage package, CancellationToken cancellationToken);

    /// <summary>
    /// Download and deserialize module metadata json from package information
    /// </summary>
    Task<ModuleMetadata?> GetModuleMetadata(ModuleDependencyPackage package, CancellationToken cancellationToken);

    /// <summary>
    /// Download and deserialize module metadata json from artifactInfo
    /// </summary>
    Task<ModuleMetadata?> GetModuleMetadata(ModuleArtifactInfo artifactInfo, CancellationToken cancellationToken);

    /// <summary>
    /// Queries specifically for module binary assets (e.g., .zip files) for the current RID.
    /// </summary>
    Task<List<ModuleArtifactInfo>> QueryModuleAssets(CancellationToken cancellationToken);

    /// <summary>
    /// Queries specifically for module metadata assets (e.g., .json files),
    /// optionally filtering by SDK version and pre-release status.
    /// </summary>
    Task<List<ModuleArtifactInfo>> QueryModuleMetadataArtifacts(Version? sdkVersion, bool includePreRelease, CancellationToken cancellationToken);

    /// <summary>
    /// Queries for the single latest module metadata asset matching the specified criteria.
    /// </summary>
    Task<ModuleArtifactInfo?> QueryLatestModuleMetadataArtifact(Version sdkVersion, string packageName, bool includePreRelease, CancellationToken cancellationToken);
}
