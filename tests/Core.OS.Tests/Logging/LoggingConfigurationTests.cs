using System.Runtime.InteropServices;
using Core.OS.Logging;
using Core.OS.Tests.Extensions;
using Core.Shared;
using Core.Shared.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sdk.Testing.Backend;
using Serilog;

namespace Core.OS.Tests.Logging;

public class LoggingConfigurationTests
{
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
            LoggingConfiguration.SetupStaticStartupLogger(config);

            // Assert
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
            var host = builder.ConfigureServices((context, services) =>
            {
                services.ConfigureLogging(context.Configuration);
            }).Build();

            // Assert
            host.Services.GetService<ILogLevelSwitch>().Should().NotBeNull();
            host.Services.GetService<ILoggerFactory>().Should().NotBeNull();
            host.Services.GetService<ILogger<LoggingConfigurationTests>>().Should().NotBeNull();
        }
    }
}
