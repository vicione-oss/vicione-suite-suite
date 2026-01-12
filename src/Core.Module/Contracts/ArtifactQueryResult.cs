using System.Text.Json.Serialization;
using Sdk.Backend.Artifacts;

namespace Core.Module.Contracts;

/// <inheritdoc />
internal class ArtifactQueryResult : IArtifactQueryResult
{
    /// <inheritdoc />
    [JsonIgnore]
    public IReadOnlyCollection<IArtifact> Artifacts => [.. Results];

    /// <inheritdoc />
    [JsonIgnore]
    public IReadOnlyCollection<IArtifactQueryRange>? Ranges => WrapperRanges;

    /// <inheritdoc />
    [JsonIgnore]
    public IReadOnlyCollection<IArtifactQueryError>? Errors => WrapperErrors;

    public List<ArtifactQueryRange>? WrapperRanges { get; set; }

    public List<Artifact> Results { get; set; } = [];

    public List<ArtifactQueryError>? WrapperErrors { get; set; }
}
