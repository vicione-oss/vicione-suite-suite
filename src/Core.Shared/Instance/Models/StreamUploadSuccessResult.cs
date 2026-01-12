namespace Core.Shared.Instance.Models;

/// <inheritdoc cref="IStreamUploadResult"/>
/// <param name="message">Success message</param>
public class StreamUploadSuccessResult(string destinationFile) : IStreamUploadResult
{
    /// <summary>
    /// File resulting from the upload operation
    /// </summary>
    public string DestinationFile => destinationFile;
}
