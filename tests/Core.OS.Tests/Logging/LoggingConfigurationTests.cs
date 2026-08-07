using System.IO.Abstractions.TestingHelpers;
using System.Runtime.InteropServices;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.Logging;
using Core.OS.Tests.Extensions;
using Core.Shared;
using Core.Shared.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sdk.Instance;
using Sdk.Testing.Backend;
using Serilog;

namespace Core.OS.Tests.Logging;

public class LoggingConfigurationTests
{
    private static InstanceOptions CreateInstanceOptions()
        => new()
        {
            HomeDirectory = "AppData",
            CacheDirectory = "Cache",
            BackupDirectory = "Backup",
            Type = InstanceType.Master,
        };

    [Collection(StaticSerilogLogger.Name)]
    public sealed class SetupStaticStartupLogger : LoggingConfigurationTests
    {
        [Fact]
        public void Should_create_log_instance()
        {
            // Arrange
            var config = new TestConfig()
                .ConfigureLogging(s =>
                {
                    s.LogPath = "AppData";
                    s.LogTargets = ["Console"];
                })
                .BuildConfiguration();

            // Act
            LoggingConfiguration.SetupStaticStartupLogger(config, new MockFileSystem(), CreateInstanceOptions());

            // Assert
            Log.Logger.Should().NotBeNull();
        }

        [Fact]
        public void Should_not_throw_when_open_telemetry_target_is_configured_without_endpoint()
        {
            // Arrange
            var config = new TestConfig()
                .ConfigureLogging(s =>
                {
                    s.LogPath = "AppData";
                    s.LogTargets = ["Console", "OpenTelemetry"];
                })
                .BuildConfiguration();

            // Act
            var act = () => LoggingConfiguration.SetupStaticStartupLogger(config, new MockFileSystem(), CreateInstanceOptions());

            // Assert
            act.Should().NotThrow();
        }

        [Theory]
        [InlineData("not-a-valid-url")]
        [InlineData("localhost:4317")]
        [InlineData("   ")]
        public void Should_not_throw_when_open_telemetry_endpoint_is_invalid(string endpoint)
        {
            // Arrange
            var config = new TestConfig()
                .ConfigureLogging(s =>
                {
                    s.LogPath = "AppData";
                    s.LogTargets = ["Console", "OpenTelemetry"];
                })
                .SetSetting("OTEL_EXPORTER_OTLP_ENDPOINT", endpoint)
                .BuildConfiguration();

            // Act
            var act = () => LoggingConfiguration.SetupStaticStartupLogger(config, new MockFileSystem(), CreateInstanceOptions());

            // Assert
            act.Should().NotThrow();
        }

        [Theory]
        [InlineData("no-separator")]
        [InlineData("=value-without-key")]
        public void Should_not_throw_when_open_telemetry_headers_are_malformed(string headers)
        {
            // Arrange
            var config = new TestConfig()
                .ConfigureLogging(s =>
                {
                    s.LogPath = "AppData";
                    s.LogTargets = ["Console", "OpenTelemetry"];
                })
                .SetSetting("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317")
                .SetSetting("OTEL_EXPORTER_OTLP_HEADERS", headers)
                .BuildConfiguration();

            // Act
            var act = () => LoggingConfiguration.SetupStaticStartupLogger(config, new MockFileSystem(), CreateInstanceOptions());

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void Should_warn_when_the_open_telemetry_queue_limit_is_not_positive()
        {
            // Arrange
            var config = new TestConfig()
                .ConfigureLogging(s =>
                {
                    s.LogPath = "AppData";
                    s.LogTargets = ["Console", "OpenTelemetry"];
                })
                .SetSetting("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317")
                .SetSetting("Logging:OpenTelemetry:QueueLimit", "0")
                .BuildConfiguration();

            using var console = new StringWriter();
            var previousOut = Console.Out;
            Console.SetOut(console);

            // Act
            try
            {
                LoggingConfiguration.SetupStaticStartupLogger(config, new MockFileSystem(), CreateInstanceOptions());
            }
            finally
            {
                Console.SetOut(previousOut);
            }

            // Assert
            console.ToString().Should().Contain(nameof(LoggingOpenTelemetryOptions.QueueLimit));
        }

        [Fact]
        public void Should_create_logger_when_open_telemetry_is_fully_configured()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var instanceOptions = CreateInstanceOptions();
            var instanceIdFilePath = fileSystem.GetLocalInstanceIdFilePath(instanceOptions);
            fileSystem.AddFile(instanceIdFilePath, new MockFileData($"{Guid.NewGuid()}\n"));

            var config = new TestConfig()
                .ConfigureLogging(s =>
                {
                    s.LogPath = "AppData";
                    s.LogTargets = ["Console", "OpenTelemetry"];
                })
                .SetSetting("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4318")
                .SetSetting("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf")
                .SetSetting("OTEL_EXPORTER_OTLP_HEADERS", "x-api-key=secret%20value")
                .BuildConfiguration();

            // Act
            var act = () => LoggingConfiguration.SetupStaticStartupLogger(config, fileSystem, instanceOptions);

            // Assert
            act.Should().NotThrow();
            Log.Logger.Should().NotBeNull();
        }
    }

    public sealed class GetLoggingSettings : LoggingConfigurationTests
    {
        [Fact]
        public void Should_throw_on_missing_section()
        {
            // Arrange
            var config = new TestConfig(false).BuildConfiguration();

            // Act + Assert
            var act = config.GetLoggingSettings;
            act.Should().Throw<ConfigurationException>();
        }

        [Fact]
        public void Should_configure_settings()
        {
            // Arrange
            var config = new TestConfig()
                .ConfigureLogging(s =>
                {
                    s.LogPath = "AppData";
                    s.LogTargets = ["Console", "Logfile", "Journal"];
                    s.LogLevel = new LoggingLogLevelOptions
                    {
                        Default = LogLevel.Debug,
                        Microsoft = LogLevel.Debug
                    };
                    if (s.Resources?.Memory is not null)
                    {
                        s.Resources.Memory.LimitInMb = 400;
                        s.Resources.Memory.LimitInPercent = 80;
                    }
                })
                .BuildConfiguration();

            // Act
            var settings = config.GetLoggingSettings();

            // Assert
            settings.Should().NotBeNull();
            settings.LogPath.Should().Be("AppData");
            settings.LogTargets.Should().HaveCount(3);
            settings.LogLevel!.Default.Should().Be(LogLevel.Debug);
            settings.Resources!.Memory!.LimitInMb.Should().Be(400);
            settings.Resources!.Memory!.LimitInPercent.Should().Be(80);
        }
    }

    [Collection(StaticSerilogLogger.Name)]
    public sealed class ConfigureLogging : LoggingConfigurationTests
    {
        [Fact]
        public void Should_setup_serilog_settings_and_services()
        {
            // Arrange
            var builder = Host.CreateDefaultBuilder();

            builder.ConfigureAppConfiguration(config =>
            {
                var settings = new Dictionary<string, string>();

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    settings["Logging:LogTargets:0"] = "Journal";
                }
                else
                {
                    settings["Logging:LogTargets:0"] = "Console"; // Fallback
                }
                config.AddInMemoryCollection(settings!);
            });

            // Act
            var instanceOptions = CreateInstanceOptions();

            var host = builder.ConfigureServices((context, services) =>
            {
                services.ConfigureLogging(context.Configuration, new MockFileSystem(), instanceOptions);
            }).Build();

            // Assert
            host.Services.GetService<ILogLevelSwitch>().Should().NotBeNull();
            host.Services.GetService<ILoggerFactory>().Should().NotBeNull();
            host.Services.GetService<ILogger<LoggingConfigurationTests>>().Should().NotBeNull();
        }
    }
}
