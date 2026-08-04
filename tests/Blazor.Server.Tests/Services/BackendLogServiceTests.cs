using Blazor.Server.Backend.Services;
using Core.Shared.Logging;
using Microsoft.Extensions.Logging;

namespace Blazor.Server.Tests.Services;

public class BackendLogServiceTests
{
    private readonly ILogLevelSwitch _logLevelSwitch = Substitute.For<ILogLevelSwitch>();

    public sealed class SetLogLevel : BackendLogServiceTests
    {
        [Fact]
        public async Task Should_set_log_level_from_log_level_switch()
        {
            // Arrange
            var backendLogService = new BackendLogService(_logLevelSwitch);

            // Act
            await backendLogService.SetLogLevel(LogLevel.Debug);

            // Assert
            _logLevelSwitch.Received().LogLevel = LogLevel.Debug;
        }
    }

    public sealed class GetLogLevel : BackendLogServiceTests
    {
        [Fact]
        public async Task Should_return_log_level_from_log_level_switch()
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
