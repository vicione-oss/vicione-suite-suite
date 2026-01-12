namespace Core.Shared.Instance.Models;

/// <inheritdoc cref="IStreamUploadResult"/>
/// <param name="message">Error message</param>
public class StreamUploadErrorResult(string message, int? errorCode = null) : IStreamUploadResult
{
    /// <summary>
    /// Error message
    /// </summary>
    public string Message => message;

    /// <summary>
    /// Optional error code
    /// </summary>
    public int? ErrorCode => errorCode;
}
