using Core.Shared.Instance.Models;

namespace Core.Shared.Instance.Services;

/// <summary>
/// Handler for uploading a stream to a file
/// </summary>
public interface IStreamUploadHandler // Maybe this could be in SDK with separate implementations for both hosting models
{
    /// <summary>
    /// Raised to notify about progress in <see cref="Execute(Stream, string, CancellationToken)"/>
    /// </summary>
    Func<IStreamUploadProgress, Task>? OnProgress { get; set; }

    /// <summary>
    /// Uploads the given <paramref name="stream"/> to a file named <paramref name="filename"/>
    /// </summary>
    /// <param name="stream">Stream to upload</param>
    /// <param name="filename">Name of the file resulting from the upload</param>
    /// <exception cref="OperationCanceledException" />
    Task<IStreamUploadResult> Execute(Stream stream, string filename, CancellationToken cancellationToken = default);
}
