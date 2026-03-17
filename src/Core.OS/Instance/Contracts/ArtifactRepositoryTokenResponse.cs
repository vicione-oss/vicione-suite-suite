using System.Text.Json.Serialization;

namespace Core.OS.Instance.Contracts;

/// <summary>
/// {"token":"cmVmdGtuOjAxOjE4MDM2NDc4MTQ6eTdqbGNVRElLMTh0QVRVMVo3clRSV2dzWkk1","valid_until":"2026-11-23T13:00:00Z"}
/// </summary>
public class ArtifactRepositoryTokenResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("valid_until")]
    public DateTime ValidUntil { get; set; }
}
