namespace Core.OS.DataProtection;

internal static partial class SafeXmlRepsitoryLog
{
    [LoggerMessage(0, LogLevel.Debug, "Reading data from file '{FullPath}'.")]
    internal static partial void ReadingDataFromFile(this ILogger logger, string fullPath);

    [LoggerMessage(0, LogLevel.Warning, "Cannot parse key file '{FullPath}'.")]
    internal static partial void FailedReadingDataFromFile(this ILogger logger, string fullPath, Exception ex);

    [LoggerMessage(0, LogLevel.Warning, "Cannot save key file '{FullPath}'.")]
    internal static partial void FailedSavingDataFromFile(this ILogger logger, string fullPath, Exception ex);
}
