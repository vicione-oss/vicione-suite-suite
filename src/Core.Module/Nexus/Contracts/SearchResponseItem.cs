using System.Text.Json.Serialization;

namespace Core.Module.Nexus.Contracts;

public class SearchResponseItem
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("repository")]
    public required string Repository { get; set; }

    [JsonPropertyName("group")]
    public string? Group { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("assets")]
    public List<SearchResponseAsset> Assets { get; set; } = [];
}
