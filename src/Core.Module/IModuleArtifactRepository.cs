using Core.Module.Contracts;
using Sdk.Backend.Artifacts;
using Sdk.Modules;

namespace Core.Module;

/// <summary>
/// Repository of module artifacts: downloading, extracting, and querying metadata or binary assets.
/// </summary>
public interface IModuleArtifactRepository
{
    Task<ModulePackageDownloadResult[]> DownloadAndExtract(
        string modulesPath,
        IEnumerable<ModuleDependencyPackage> packages,
        CancellationToken cancellationToken);

    /// <summary>
    /// Downloads a module package and its metadata, extracting into a versioned subfolder of the modules path.
    /// </summary>
    Task<ModulePackageDownloadResult> DownloadAndExtract(
        string modulesPath,
        ModuleDependencyPackage package,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns a raw stream to the module's metadata JSON.
    /// </summary>
    Task<Stream> GetMetadataDownloadStream(
        ModuleDependencyPackage package,
        CancellationToken cancellationToken);

    Task<ModuleMetadata?> GetModuleMetadata(
        ModuleDependencyPackage package,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns <see langword="null"/> when the artifact is not a valid metadata JSON.
    /// </summary>
    Task<ModuleMetadata?> GetModuleMetadata(
        IArtifact metadataArtifact,
        CancellationToken cancellationToken);

    /// <summary>
    /// Queries binary module assets (.zip) matching the current runtime identifier.
    /// </summary>
    Task<List<IArtifact>> QueryModuleArtifacts(
        CancellationToken cancellationToken);

    /// <summary>
    /// Queries the module artifact (.zip) matching name and version of <paramref name="package"/>.
    /// </summary>
    Task<IArtifact?> QueryModuleArtifact(
        ModuleDependencyPackage package,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries module metadata artifacts (.json). A <see langword="null"/> filter argument is not applied.
    /// </summary>
    Task<List<IArtifact>> QueryModuleMetadataArtifacts(
        Version? sdkVersion,
        DateTimeOffset? modifiedAfter,
        CancellationToken cancellationToken);

    /// <summary>
    /// Queries the latest metadata-tagged artifact for the module and SDK version, excluding ci-versions.
    /// A <see langword="null"/> <paramref name="major"/> or <paramref name="minor"/> is not applied as a filter.
    /// </summary>
    Task<IArtifact?> QueryLatestModuleMetadataArtifact(
        Version sdkVersion,
        string packageName,
        int? major,
        int? minor,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the keys identifying the currently configured artifact sources. A change in the returned set
    /// indicates that sources were added or removed and any cached artifacts should be invalidated.
    /// </summary>
    IReadOnlyCollection<string> GetSourceKeys();
}
