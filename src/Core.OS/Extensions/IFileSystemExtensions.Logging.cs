namespace Core.OS.Extensions;

internal static partial class IFileSystemExtensions
{
    [LoggerMessage(LogLevel.Information, "Reset {Source} workspace '{Name}' done")]
    private static partial void LogResetWorkspaceDone(ILogger logger, string source, string? name);

    [LoggerMessage(LogLevel.Error, "Insufficient permissions to delete {Source} workspace directory")]
    private static partial void LogDeleteWorkspaceDirectoryUnauthorized(ILogger logger, Exception exception, string? source);

    [LoggerMessage(LogLevel.Error, "Failed to reset {Source} workspace '{Name}'.")]
    private static partial void LogDeleteWorkspaceDirectoryFailed(ILogger logger, Exception exception, string source, string? name);

    [LoggerMessage(LogLevel.Information, "Delete {Source} workspace file '{Name}'.")]
    private static partial void LogDeleteWorkspaceFile(ILogger logger, string source, string name);

    [LoggerMessage(LogLevel.Error, "Insufficient permissions to delete {Source} workspace file")]
    private static partial void LogDeleteWorkspaceFileUnauthorized(ILogger logger, Exception exception, string source);

    [LoggerMessage(LogLevel.Error, "Failed to delete {Source} workspace file '{Name}'.")]
    private static partial void LogDeleteWorkspaceFileFailed(ILogger logger, Exception exception, string source, string name);
}
