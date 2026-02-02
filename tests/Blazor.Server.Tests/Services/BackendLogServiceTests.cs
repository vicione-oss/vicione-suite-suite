using Blazor.Server.Backend.Services;
using AwesomeAssertions;
using Core.Shared.Logging;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Blazor.Server.Tests.Services;

public class BackendLogServiceTests
{
    private readonly ILogLevelSwitch _logLevelSwitch = Substitute.For<ILogLevelSwitch>();

    public class LogLevelTests : BackendLogServiceTests
    {
        [Fact]
        public async Task Set_loglevel_from_log_level_switch()
        {
            // Arrange
            var backendLogService = new BackendLogService(_logLevelSwitch);

            // Act 
            await backendLogService.SetLogLevel(LogLevel.Debug);

            // Assert
            _logLevelSwitch.Received().LogLevel = LogLevel.Debug;
        }

        [Fact]
        public async Task Returns_loglevel_from_log_level_switch()
        {
            // Arrange
            var backendLogService = new BackendLogService(_logLevelSwitch);
            _logLevelSwitch.LogLevel.Returns(LogLevel.Debug);

            // Act 
            var actualLogLevel = await backendLogService.GetLogLevel();

            // Assert
            actualLogLevel.Should().Be(LogLevel.Debug);
        }
    }
}
