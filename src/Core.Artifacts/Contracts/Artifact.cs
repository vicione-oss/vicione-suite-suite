using System.Diagnostics;
using System.Text.Json.Serialization;
using Sdk.Backend.Artifacts;

namespace Core.Artifacts.Contracts;

/// <inheritdoc />
[DebuggerDisplay("Repo = {Repository,nq}, Path = {Path,nq}, Name = {Name,nq}")]
internal class Artifact : IArtifact
{
    /// <inheritdoc />  
    [JsonPropertyName("modified")]
    public DateTimeOffset? Modified { get; set; }

    /// <inheritdoc />
    public IArtifactChecksum? Checksum { get; set; }

    /// <inheritdoc />
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <inheritdoc />
    [JsonPropertyName("path")]
    public required string Path { get; set; }

    /// <inheritdoc />
    [JsonPropertyName("repo")]
    public string Repository { get; set; } = string.Empty;

    /// <inheritdoc />
    [JsonPropertyName("size")]
    public long? Size { get; set; }

    /// <inheritdoc />
    [JsonPropertyName("type")]
    public ArtifactKind Kind { get; set; } = ArtifactKind.File;

    /// <inheritdoc />
    public string SourceKey { get; set; } = string.Empty;
}
