using System.Text.Json.Serialization;

namespace Core.OS.Instance.Contracts;

public class ArtifactRepositoryTokenResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("valid_until")]
    public DateTime ValidUntil { get; set; }
}
