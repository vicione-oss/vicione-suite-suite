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
    /// Architecture of this machine as named in suite package names: <c>amd64</c> or <c>arm64</c>.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">No suite packages are built for this architecture.</exception>
    string GetOSArchitecture();
}
