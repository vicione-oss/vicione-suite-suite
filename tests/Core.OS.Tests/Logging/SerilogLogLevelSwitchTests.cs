using AwesomeAssertions;
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
    public void Should_map_log_levels_bidirectionally(LogEventLevel level, LogLevel mapped)
    {
        // Arrange
        var logLevelSwitch = new SerilogLogLevelSwitch(new LoggingLevelSwitch(level));

        // Act
        var mappedLogLevel = logLevelSwitch.LogLevel;
        logLevelSwitch.LogLevel = mapped;

        // Assert
        mappedLogLevel.Should().Be(mapped);
        logLevelSwitch.WrappedBaseLoggingLevelSwitch.MinimumLevel.Should().Be(level);
    }
}
