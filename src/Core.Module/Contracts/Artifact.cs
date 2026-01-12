using System.Diagnostics;
using System.Text.Json.Serialization;
using Sdk.Backend.Artifacts;

namespace Core.Module.Contracts;

/// <inheritdoc />
[DebuggerDisplay("Repo = {Repository,nq}, Path = {Path,nq}, Name = {Name,nq}")]
internal class Artifact : IArtifact
{
    /// <inheritdoc />
    public IArtifactChecksum? Checksum { get; set; }

    [JsonPropertyName("modified")]
    /// <inheritdoc />     
    public DateTimeOffset? Modified { get; set; }

    [JsonPropertyName("name")]
    /// <inheritdoc />
    public required string Name { get; set; }

    [JsonPropertyName("path")]
    /// <inheritdoc />
    public required string Path { get; set; }

    [JsonPropertyName("repo")]
    /// <inheritdoc />
    public string Repository { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    /// <inheritdoc />
    public long? Size { get; set; }

    [JsonPropertyName("type")]
    /// <inheritdoc />
    public ArtifactKind Kind { get; set; } = ArtifactKind.File;

    /// <inheritdoc />
    public string SourceKey { get; set; } = string.Empty;
}
