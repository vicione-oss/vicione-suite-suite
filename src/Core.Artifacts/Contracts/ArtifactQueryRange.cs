using System.Diagnostics;
using System.Text.Json.Serialization;
using Sdk.Backend.Artifacts;

namespace Core.Artifacts.Contracts;

/// <inheritdoc/>
[DebuggerDisplay("StartPosition = {StartPosition,nq}, EndPosition = {EndPosition,nq}, Total = {Total,nq}")]
internal class ArtifactQueryRange : IArtifactQueryRange
{
    /// <inheritdoc/>
    [JsonPropertyName("start_pos")]
    public int StartPosition { get; set; }

    /// <inheritdoc/>
    [JsonPropertyName("end_pos")]
    public int EndPosition { get; set; }

    /// <inheritdoc/>
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <inheritdoc/>
    public string Source { get; set; } = string.Empty;
}
