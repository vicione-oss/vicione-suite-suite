using Microsoft.Extensions.Logging;

namespace Blazor.Shared.Logging.Contracts;

internal sealed class LogViewModel
{
    public List<string> LogFiles { get; } = [];

    public string? LogText { get; set; }

    public static IEnumerable<LogLevel> LogLevels => Enum.GetValues<LogLevel>();

    public LogLevel SelectedLogLevel { get; set; }

    public string? SelectedLogName { get; set; }

    public bool IsLoading { get; set; }

    public void SetLogPaths(IEnumerable<string> logFiles)
    {
        LogFiles.Clear();
        SelectedLogName = null;

        LogFiles.AddRange(logFiles//Sort files first, then directories
            .OrderBy(l => l.Contains('\\', StringComparison.InvariantCulture)
                || l.Contains('/', StringComparison.InvariantCulture))
            .ThenBy(l => l, StringComparer.InvariantCulture));

        if (LogFiles.Count > 0)
            SelectedLogName = LogFiles.FirstOrDefault(k => k.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
                ?? LogFiles.First();
    }
}
