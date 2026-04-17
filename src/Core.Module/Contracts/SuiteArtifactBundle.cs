using Sdk.Backend.Artifacts;
using Semver;

namespace Core.Module.Contracts;

/// <summary>
/// Represents a bundle of suite artifacts, consisting of a linux install package (.deb)
/// and its optional signature, as well as parsed metadata such as architecture 
/// and HostManagement version.
/// </summary>
public class SuiteArtifactBundle
{
    /// <summary>
    /// Gets the HostManagement version parsed from the artifact name
    /// </summary>
    public SemVersion? HostManagementVersion { get; init; }

    /// <summary>
    /// Gets the architecture string parsed from the artifact name, e.g. "win-x64".
    /// </summary>
    public required string Architecture { get; init; }

    /// <summary>
    /// Gets the version string parsed from the artifact name, e.g. "1.1.0".
    /// </summary>
    public required SemVersion Version { get; init; }

    /// <summary>
    /// Gets the suite package artifact associated with this bundle.
    /// </summary>
    public required IArtifact Package { get; init; }

    /// <summary>
    /// Gets or sets the optional signature artifact for the suite package.
    /// </summary>
    public IArtifact? PackageSignature { get; set; }
}

