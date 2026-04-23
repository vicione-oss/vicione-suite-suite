using Sdk.Backend.Artifacts;

namespace Core.Artifacts.Contracts;

/// <inheritdoc />
internal class ArtifactChecksum : IArtifactChecksum
{
    /// <inheritdoc />
    public string? Sha1 { get; set; }

    /// <inheritdoc />
    public string? Sha256 { get; set; }

    /// <inheritdoc />
    public string? Sha512 { get; set; }

    /// <inheritdoc />
    public string? Md5 { get; set; }
}
