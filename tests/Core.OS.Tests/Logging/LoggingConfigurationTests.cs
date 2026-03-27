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
using Xunit;

namespace Core.OS.Tests.Logging;

public class LoggingConfigurationTests
{
    [Fact]
    public void Setup_static_startup_logger_should_create_log_instance()
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
        Assert.NotNull(Log.Logger);
    }

    [Fact]
    public void Get_module_settings_should_throw_on_missing_section()
    {
        // Arrange
        var config = new TestConfig(false).BuildConfiguration();

        // Act + Assert
        Assert.Throws<ConfigurationException>(config.GetLoggingSettings);
    }

    [Fact]
    public void Add_logging_settings_should_configure_settings()
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
        Assert.NotNull(settings);
        Assert.Equal("AppData", settings.LogPath);
        Assert.Equal(3, settings.LogTargets?.Length);
        Assert.Equal(LogLevel.Debug, settings.LogLevel!.Default);
        Assert.Equal(400, settings.Resources!.Memory!.LimitInMb);
        Assert.Equal(80, settings.Resources!.Memory!.LimitInPercent);
    }

    [Fact]
    public void Configure_logging_should_setup_serilog_settings_and_services()
    {
        // Act
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

        var host = builder.ConfigureServices((context, services) =>
        {
            services.ConfigureLogging(context.Configuration);
        }).Build();

        // Assert
        Assert.NotNull(host.Services.GetService<ILogLevelSwitch>());
        Assert.NotNull(host.Services.GetService<ILoggerFactory>());
        Assert.NotNull(host.Services.GetService<ILogger<LoggingConfigurationTests>>());
    }
}
