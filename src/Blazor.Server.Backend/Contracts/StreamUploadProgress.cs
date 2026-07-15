using Sdk.Client.Models;

namespace Blazor.Server.Backend.Contracts;

internal sealed class StreamUploadProgress : IStreamUploadProgress
{
    public required string Path { get; init; }
    public required string Filename { get; init; }
    public string? DestinationFile { get; set; }
    public long BytesTotal { get; init; }
    public long BytesUploaded { get; set; }
}
