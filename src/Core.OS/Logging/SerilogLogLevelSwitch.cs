using Core.Shared.Logging;
using Serilog.Core;
using Serilog.Events;

namespace Core.OS.Logging;

public sealed class SerilogLogLevelSwitch(LoggingLevelSwitch loggingLevelSwitch) : ILogLevelSwitch
{
    public LoggingLevelSwitch WrappedBaseLoggingLevelSwitch { get; } = loggingLevelSwitch;

    public LogLevel LogLevel
    {
        get
        {
            return Map(WrappedBaseLoggingLevelSwitch.MinimumLevel);
        }
        set
        {
            WrappedBaseLoggingLevelSwitch.MinimumLevel = ToLogEventLevel(value);
        }
    }

    public static LogEventLevel ToLogEventLevel(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Critical => LogEventLevel.Fatal,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Trace => LogEventLevel.Verbose,
            LogLevel.Warning => LogEventLevel.Warning,
            _ => LogEventLevel.Fatal
        };
    }

    private static LogLevel Map(LogEventLevel logLevel)
    {
        return logLevel switch
        {
            LogEventLevel.Fatal => LogLevel.Critical,
            LogEventLevel.Debug => LogLevel.Debug,
            LogEventLevel.Error => LogLevel.Error,
            LogEventLevel.Information => LogLevel.Information,
            LogEventLevel.Verbose => LogLevel.Trace,
            LogEventLevel.Warning => LogLevel.Warning,
            _ => LogLevel.None
        };
    }
}
