namespace Core.OS.Instance.Services;

/// <summary>
/// Thrown when a backup holds a system configuration in a format the Suite can no longer restore.
/// </summary>
public sealed class UnsupportedBackupFormatException : InvalidOperationException
{
    private const string DefaultMessage =
        "The system configuration in this backup has a format that is no longer supported, so the backup cannot be restored. Please create a new backup.";

    public UnsupportedBackupFormatException() : base(DefaultMessage)
    {
    }

    public UnsupportedBackupFormatException(string message) : base(message)
    {
    }

    public UnsupportedBackupFormatException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
