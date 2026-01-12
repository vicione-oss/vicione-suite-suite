namespace Core.Shared.Instance.Models;

/// <summary>
/// Progress of a stream upload
/// </summary>
public interface IStreamUploadProgress
{    /// <summary>
     /// Path associated with the stream upload
     /// </summary>
    string Path { get; }

    /// <summary>
    /// Filename associated with the stream upload
    /// </summary>
    string Filename { get; }

    /// <summary>
    /// File resulting from the stream upload
    /// </summary>
    string? DestinationFile { get; }

    /// <summary>
    /// Bytes in total being uploaded
    /// </summary>
    long BytesTotal { get; }

    /// <summary>
    /// Bytes already uploaded
    /// </summary>
    long BytesUploaded { get; }
}
