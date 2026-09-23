using Core.Module.Contracts;
using Sdk.Backend.Artifacts;

namespace Core.Module;

/// <summary>
/// Repository of suite artifacts: packages and their signatures.
/// </summary>
public interface ISuiteArtifactRepository
{
    /// <summary>
    /// Downloads the suite and signature package and validates it against
    /// <see cref="Options.ArtifactRepositoryOptions.PublicKeys"/>; one matching key is enough.
    /// </summary>
    Task<SuitePackageDownloadResult> DownloadAndValidate(string downloadPath, string suiteArtifactName, string signatureArtifactName, CancellationToken cancellationToken);

    /// <summary>
    /// Downloads the suite and signature package of the bundle and validates it against
    /// <see cref="Options.ArtifactRepositoryOptions.PublicKeys"/>; one matching key is enough.
    /// </summary>
    Task<SuitePackageDownloadResult> DownloadAndValidate(string downloadPath, SuiteArtifactBundle suiteBundle, CancellationToken cancellationToken);

    /// <summary>
    /// Queries all suite artifacts, packages and signatures, without filtering by HostManagement version.
    /// </summary>
    Task<IReadOnlyCollection<IArtifact>> QueryAllSuiteArtifacts(CancellationToken cancellationToken);

    /// <summary>
    /// Queries the suite artifact bundles compatible with the given HostManagement version.
    /// </summary>
    Task<IReadOnlyCollection<SuiteArtifactBundle>> QuerySuiteArtifactBundles(
        Version minimumHostManagementVersion,
        bool includeUnsignedPackages = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current operating system architecture string as LowerInvariant, e.g. "arm64" or "amd64".
    /// </summary>
    string GetOSArchitectureFilter();
}
