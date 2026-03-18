using Core.Module.Contracts;
using Sdk.Backend.Artifacts;

namespace Core.Module;

/// <summary>
/// Defines an abstraction for interacting with a repository of suite artifacts (packages and their signatures).
/// </summary>
public interface ISuiteArtifactRepository
{
    /// <summary>
    /// Download suite and signature package, referenced by <paramref name="suiteArtifactName"/> and 
    /// validates it against <see cref="Options.ArtifactRepositoryOptions.PublicKeys"/>. At least one
    /// key has to validate the package it's assumed as valid.
    /// </summary>
    /// <returns>
    /// The path of the downloaded and validated suite package
    /// </returns>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>    
    Task<SuitePackageDownloadResult> DownloadAndValidate(string downloadPath, string suiteArtifactName, string signatureArtifactName, CancellationToken cancellationToken);

    /// <summary>
    /// Download suite and signature package, referenced by <paramref name="suiteBundle"/> and 
    /// validates it against <see cref="Options.ArtifactRepositoryOptions.PublicKeys"/>. At least one
    /// key has to validate the package it's assumed as valid.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>    
    Task<SuitePackageDownloadResult> DownloadAndValidate(string downloadPath, SuiteArtifactBundle suiteBundle, CancellationToken cancellationToken);

    /// <summary>
    /// Queries all available suite artifacts, including packages and signatures,
    /// without filtering by HostManagement version.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>   
    Task<IReadOnlyCollection<IArtifact>> QueryAllSuiteArtifacts(CancellationToken cancellationToken);

    /// <summary>
    /// Queries all suite artifact bundles that are compatible with the given HostManagement version.
    /// </summary>
    /// <param name="minimumhostManagementVersion">The minimal required version of HostManagement to match against.</param>
    /// <param name="includeUnsignedPackages">
    /// Set to <see langword="true"/> to include unsigned packages in the results; otherwise, only signed packages are returned.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>    
    Task<IReadOnlyCollection<SuiteArtifactBundle>> QuerySuiteArtifactBundles(
        Version minimumHostManagementVersion,
        bool includeUnsignedPackages = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current operating system architecture string as LowerInvariant, e.g. "arm64" or "amd64".
    /// </summary>
    string GetOSArchitectureFilter();
}
