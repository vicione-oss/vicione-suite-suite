using System.Text.Json.Serialization;

namespace Core.Module.Nexus.Contracts;

public class SearchItemsResponse : IContinuationResponse
{
    [JsonPropertyName("items")]
    public List<SearchResponseItem> Items { get; set; } = [];

    [JsonPropertyName("continuationToken")]
    public string? ContinuationToken { get; set; }
}
