using System.Text.Json.Serialization;
using Sdk.Backend.ArtifactApi;
using Sdk.Messaging;

namespace Core.Module.Contracts;

/// <inheritdoc />
internal class ArtifactQueryResult : IArtifactQueryResult
{
    /// <inheritdoc />
    [JsonIgnore]
    public IReadOnlyCollection<IArtifactItem> Artifacts => Results.ToArray();

    [JsonPropertyName("results")]
    public List<ArtifactItem> Results { get; set; } = [];

    /// <inheritdoc />
    public ErrorInfo? ErrorInfo { get; set; }
}
