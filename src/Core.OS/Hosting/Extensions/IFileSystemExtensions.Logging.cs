namespace Core.OS.Hosting.Extensions;

internal static partial class IFileSystemExtensions
{
    [LoggerMessage(LogLevel.Information, "Initialized data version to '{SuiteVersion}'")]
    private static partial void LogDataVersionInitialized(ILogger logger, string suiteVersion);

    [LoggerMessage(LogLevel.Warning, "Empty data version restored to '{SuiteVersion}'")]
    private static partial void LogDataVersionEmptyRestored(ILogger logger, string suiteVersion);

    [LoggerMessage(LogLevel.Information, "Updated data version to '{SuiteVersion}'")]
    private static partial void LogDataVersionUpdated(ILogger logger, string suiteVersion);

    [LoggerMessage(LogLevel.Warning, "Downgraded data version to '{SuiteVersion}'")]
    private static partial void LogDataVersionDowngraded(ILogger logger, string suiteVersion);

    [LoggerMessage(LogLevel.Warning, "Software version should be updated from '{SuiteVersion}' to at least '{PersistedVersion}'")]
    private static partial void LogDataVersionDowngradeDetected(ILogger logger, string suiteVersion, string persistedVersion);
}
