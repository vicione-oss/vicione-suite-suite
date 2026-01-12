using System.Text.Json.Serialization;

namespace Core.Module.Nexus.Contracts;

public class SearchAssetsResponse : IContinuationResponse
{
    [JsonPropertyName("items")]
    public List<SearchResponseAsset> Items { get; set; } = [];

    [JsonPropertyName("continuationToken")]
    public string? ContinuationToken { get; set; }
}
