using Sdk.Backend.Artifacts;
using Semver;

namespace Core.Module.Contracts;

/// <summary>
/// A linux install package (.deb) with its optional signature, plus the architecture and
/// HostManagement version parsed from the artifact name.
/// </summary>
public class SuiteArtifactBundle
{
    /// <summary>
    /// HostManagement version parsed from the artifact name.
    /// </summary>
    public SemVersion? HostManagementVersion { get; init; }

    /// <summary>
    /// Architecture parsed from the artifact name, e.g. "win-x64".
    /// </summary>
    public required string Architecture { get; init; }

    /// <summary>
    /// Suite version parsed from the artifact name, e.g. "1.1.0".
    /// </summary>
    public required SemVersion Version { get; init; }

    public required IArtifact Package { get; init; }

    public IArtifact? PackageSignature { get; set; }
}

