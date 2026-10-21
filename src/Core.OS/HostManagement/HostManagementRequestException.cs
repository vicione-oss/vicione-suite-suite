namespace Core.OS.HostManagement;

/// <summary>
/// Thrown when HostManagement answers a request with a plain response that carries only the reason, because it does not know the topic,
/// has no handler for it, or the handler threw or was canceled.
/// </summary>
/// <remarks>
/// Derives from <see cref="InvalidOperationException"/>, so callers catching the connect failures also catch this one.
/// </remarks>
public sealed class HostManagementRequestException : InvalidOperationException
{
    public string? Topic { get; }

    public string? Reason { get; }

    public HostManagementRequestException()
    {
    }

    public HostManagementRequestException(string message) : base(message)
    {
    }

    public HostManagementRequestException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public HostManagementRequestException(string topic, string? reason, Exception? innerException)
        : base($"HostManagement request '{topic}' failed: {reason}", innerException)
    {
        Topic = topic;
        Reason = reason;
    }
}
