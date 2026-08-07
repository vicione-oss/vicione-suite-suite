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
    [ValidateObjectMembers]
    public LoggingSpamGuardOptions? SpamGuard { get; set; }
    [ValidateObjectMembers]
    public LoggingOpenTelemetryOptions? OpenTelemetry { get; set; }
}

public sealed class LoggingOpenTelemetryOptions
{
    public const string ConfigSection = "OpenTelemetry";

    /// <summary>
    /// Floor for events exported via OTLP. Keeps runtime log level changes
    /// (e.g. enabling debug logging for local troubleshooting) from streaming
    /// verbose logs over potentially metered edge device uplinks.
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Bounds the events held in memory while the OTLP endpoint is unreachable,
    /// which is a normal condition for edge devices with intermittent connectivity.
    /// </summary>
    public int QueueLimit { get; set; } = 10_000;
}

public sealed class LoggingSpamGuardOptions
{
    public const string ConfigSection = "SpamGuard";

    public bool? Enabled { get; set; }
    public int? WindowSizeSeconds { get; set; }
    [Range(1, int.MaxValue)]
    public int? SummaryCountThreshold { get; set; }
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
