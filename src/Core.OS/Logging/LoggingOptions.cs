using System.ComponentModel.DataAnnotations;
using Core.Shared.Logging;
using Microsoft.Extensions.Options;

namespace Core.OS.Logging;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819: Properties should not return arrays", Justification = "DTO")]
public sealed class LoggingOptions : ILogOptions
{
    public const string ConfigSection = "Logging";

    public string? LogPath { get; set; }
    public string[]? LogTargets { get; set; }
    [ValidateObjectMembers]
    public LoggingResourceOptions? Resources { get; set; }
    [ValidateObjectMembers]
    public LoggingLogLevelOptions? LogLevel { get; set; }
}

public sealed class LoggingResourceOptions
{
    public const string ConfigSection = "Resources";

    [ValidateObjectMembers]
    public LoggingMemoryOptions? Memory { get; set; }
}

public sealed class LoggingMemoryOptions
{
    public const string ConfigSection = "Memory";

    [Range(1, int.MaxValue)]
    public int LimitInMb { get; set; } = 500;

    [Range(1, 100)]
    public int LimitInPercent { get; set; } = 75;
}

public sealed class LoggingLogLevelOptions
{
    public const string ConfigSection = "LogLevel";

    public LogLevel Default { get; set; } = LogLevel.Information;

    public LogLevel Microsoft { get; set; } = LogLevel.Error;
}
