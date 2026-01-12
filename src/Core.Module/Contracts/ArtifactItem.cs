using System.Diagnostics;
using Sdk.Backend.ArtifactApi;

namespace Core.Module.Contracts;

/// <inheritdoc />
[DebuggerDisplay("Repo = {Repo,nq}, Path = {Path,nq}, Name = {Name,nq}")]
internal class ArtifactItem : IArtifactItem
{
    /// <inheritdoc />
    public IArtifactChecksum? Checksum { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? Modified { get; set; }

    /// <inheritdoc />
    public required string Name { get; set; }

    /// <inheritdoc />
    public required string Path { get; set; }

    /// <inheritdoc />
    public string Repo { get; set; } = string.Empty;

    /// <inheritdoc />
    public long? Size { get; set; }
}
