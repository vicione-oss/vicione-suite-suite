namespace Core.OS.Instance.Extensions;

internal static partial class IFileSystemExtensions
{
    [LoggerMessage(LogLevel.Information, "Recovery mode is disabled by configuration")]
    private static partial void LogRecoveryModeDisabled(ILogger logger);

    [LoggerMessage(LogLevel.Warning, "Failed to restore recovery state")]
    private static partial void LogRecoveryStateReadFailed(ILogger logger);

    [LoggerMessage(LogLevel.Information, "Startup counter increased to {Count}")]
    private static partial void LogStartupCounterIncreased(ILogger logger, int count);

    [LoggerMessage(LogLevel.Critical, "Recovery mode was already applied but the suite crashed {Startups} more times within {Minutes} minutes. Entering terminal failed state")]
    private static partial void LogRecoveryExhausted(ILogger logger, int startups, int minutes);

    [LoggerMessage(LogLevel.Warning, "Fallback to recovery mode after {Startups} startups")]
    private static partial void LogFallingBackToRecovery(ILogger logger, int startups);

    [LoggerMessage(LogLevel.Error, "Failed to read recovery state. Content: {FileContent}")]
    private static partial void LogReadRecoveryStateContentFailed(ILogger logger, Exception exception, string fileContent);

    [LoggerMessage(LogLevel.Error, "Failed to read recovery state. File '{FilePath}' is corrupt")]
    private static partial void LogReadRecoveryStateFileFailed(ILogger logger, Exception exception, string filePath);

    [LoggerMessage(LogLevel.Information, "Attempting to delete recovery state file")]
    private static partial void LogDeletingRecoveryStateFile(ILogger logger);

    [LoggerMessage(LogLevel.Critical, "Failed to delete recovery state")]
    private static partial void LogDeleteRecoveryStateFailed(ILogger logger, Exception exception);
}
