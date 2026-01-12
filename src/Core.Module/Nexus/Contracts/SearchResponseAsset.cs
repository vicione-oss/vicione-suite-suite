using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Core.Module.Nexus.Contracts;

[DebuggerDisplay("{Path} - modified {LastModified}")]
public class SearchResponseAsset
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("downloadUrl")]
    public Uri? DownloadUrl { get; set; }

    [JsonPropertyName("contentType")]
    public string? ContentType { get; set; }

    [JsonPropertyName("lastModified")]
    public DateTime LastModified { get; set; }

    [JsonPropertyName("path")]
    public required string Path { get; set; }

    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }

    [JsonPropertyName("checksum")]
    public SearchResponseCheckSum? CheckSum { get; set; }
}
