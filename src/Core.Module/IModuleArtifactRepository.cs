using Core.Module.Contracts;
using Sdk.Backend.Artifacts;
using Sdk.Modules;

namespace Core.Module;

/// <summary>
/// Defines an abstraction for interacting with a repository of module artifacts, 
/// including downloading, extracting, and querying metadata or binary assets.
/// </summary>
public interface IModuleArtifactRepository
{
    /// <summary>
    /// Downloads and extracts multiple module packages into the specified directory.
    /// </summary>
    /// <param name="modulesPath">The target directory where modules will be extracted.</param>
    /// <param name="packages">The list of module packages to download and extract.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<ModulePackageDownloadResult[]> DownloadAndExtract(
        string modulesPath,
        IEnumerable<ModuleDependencyPackage> packages,
        CancellationToken cancellationToken);

    /// <summary>
    /// Downloads a single module package and its corresponding metadata file, then extracts it 
    /// to a versioned subfolder under the specified modules directory.
    /// </summary>
    /// <param name="modulesPath">The target root directory for extraction.</param>
    /// <param name="package">The module package to download.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<ModulePackageDownloadResult> DownloadAndExtract(
        string modulesPath,
        ModuleDependencyPackage package,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves a raw stream to the module's metadata JSON file based on its dependency package information.
    /// </summary>
    /// <param name="package">The package descriptor identifying the module.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<Stream> GetMetadataDownloadStream(
        ModuleDependencyPackage package,
        CancellationToken cancellationToken);

    /// <summary>
    /// Downloads and deserializes the module metadata JSON using the specified package information.
    /// </summary>
    /// <param name="package">The package descriptor identifying the module.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>    
    Task<ModuleMetadata?> GetModuleMetadata(
        ModuleDependencyPackage package,
        CancellationToken cancellationToken);

    /// <summary>
    /// Downloads and deserializes the module metadata JSON using the given artifact reference.
    /// </summary>
    /// <param name="metadataArtifact">The artifact referencing the metadata JSON.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="ModuleMetadata"/> instance if the artifact is valid; otherwise <see langword="null"/>.
    /// </returns>
    Task<ModuleMetadata?> GetModuleMetadata(
        IArtifact metadataArtifact,
        CancellationToken cancellationToken);

    /// <summary>
    /// Queries the repository for binary module assets (.zip files) matching the current runtime identifier (RID).
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<List<IArtifact>> QueryModuleArtifacts(
        CancellationToken cancellationToken);

    /// <summary>
    /// Queries the repository for a module artifact (.zip) matching name and version of <paramref name="package"/>
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<IArtifact?> QueryModuleArtifact(
        ModuleDependencyPackage package,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries the repository for module metadata artifacts (.json files), 
    /// optionally filtering by SDK version and pre-release availability.
    /// </summary>
    /// <param name="sdkVersion">The minimum SDK version required for compatibility, or <see langword="null"/> to skip filtering.</param>
    /// <param name="modifiedAfter">Filter results to get only ones modified after datetime, or <see langword="null"/> to skip filtering.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<List<IArtifact>> QueryModuleMetadataArtifacts(
        Version? sdkVersion,
        DateTimeOffset? modifiedAfter,
        CancellationToken cancellationToken);

    /// <summary>
    /// Queries the latest available metadata artifact for the specified module name and SDK version,
    /// optionally including pre-release versions.
    /// </summary>
    /// <param name="sdkVersion">The SDK version to match against.</param>
    /// <param name="packageName">The name of the module package.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<IArtifact?> QueryLatestModuleMetadataArtifact(
        Version sdkVersion,
        string packageName,
        CancellationToken cancellationToken);
}
