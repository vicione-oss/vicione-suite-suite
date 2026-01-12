using Core.OS.Logging;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace Core.OS.Tests.Logging;

public class SerilogLogLevelSwitchTests
{
    [Theory]
    [InlineData(LogEventLevel.Verbose, LogLevel.Trace)]
    [InlineData(LogEventLevel.Debug, LogLevel.Debug)]
    [InlineData(LogEventLevel.Information, LogLevel.Information)]
    [InlineData(LogEventLevel.Warning, LogLevel.Warning)]
    [InlineData(LogEventLevel.Error, LogLevel.Error)]
    [InlineData(LogEventLevel.Fatal, LogLevel.Critical)]
    public void Tests_should_cover_all_log_level_switch_things(LogEventLevel level, LogLevel mapped)
    {
        // Arrange
        var logLevelSwitch = new SerilogLogLevelSwitch(new LoggingLevelSwitch(level));

        // Act + Assert
        Assert.Equal(mapped, logLevelSwitch.LogLevel);

        logLevelSwitch.LogLevel = mapped;

        Assert.Equal(level, logLevelSwitch.WrappedBaseLoggingLevelSwitch.MinimumLevel);
    }
}
