using Core.OS.Extensions;
using Core.OS.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Core.OS.Tests.Logging;

public class LoggingOptionsTests
{
    public sealed class OpenTelemetryQueueLimit : LoggingOptionsTests
    {
        /// <summary>
        /// An out-of-range logging knob must not take an unattended device out of service:
        /// the sink clamps the value instead (see <c>LoggingConfiguration.AddOpenTelemetrySink</c>).
        /// </summary>
        [Fact]
        public void Should_not_fail_startup_validation_when_it_is_not_positive()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:OpenTelemetry:QueueLimit"] = "0",
            });

            builder.Services.AddSuiteOptions<LoggingOptions>(LoggingOptions.ConfigSection);
            var host = builder.Build();

            // Act
            var failures = host.GetInvalidOptions();

            // Assert
            failures.Should().BeNull();
        }
    }
}
