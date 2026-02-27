using System.Globalization;
using Core.Shared;
using Core.Shared.Logging;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.Journal;
using Serilog.Sinks.SystemConsole.Themes;
using Serilog.Templates;

namespace Core.OS.Logging;

internal static class LoggingConfiguration
{
    private const string LogFileNameTemplate = "suite.log";

    private const string LogTemplate =
        "[{Timestamp:dd.MM.yyyy HH:mm:ss.FFF zzz} {Level:u3}] {CorrelationId} {SourceContext} {NewLine}\t{Message:lj}{NewLine}{Exception}";

    private const string SyslogTemplate =
        "{SourceContext} {Message:lj} {Exception}";

    private const string JournalLogtemplate =
        "{#if SourceContext is not null}[{SourceContext}]{#if ModuleId is not null}({ModuleId}){#end} {#end}{@m}\n{@x}";

    private static readonly SerilogLogLevelSwitch _loggingLevelSwitch
        = new(new LoggingLevelSwitch(LogEventLevel.Warning));

    public static LoggingOptions GetLoggingSettings(this IConfiguration config)
        => config.GetSection(LoggingOptions.ConfigSection).Get<LoggingOptions>() ?? throw new ConfigurationException(LoggingOptions.ConfigSection);

    /// <summary>
    /// creates the logger used on startup methods
    /// https://stackoverflow.com/questions/66045967/serilog-static-logger-is-silentlogger-when-theres-an-exception-in-masstransit-a
    /// </summary>
    internal static void SetupStaticStartupLogger(IConfiguration configuration)
    {
        SetLoggingSwitchSwitchLogLevel(configuration);

        Log.Logger = new LoggerConfiguration()
                .MinimumLevel
                .ControlledBy(_loggingLevelSwitch.WrappedBaseLoggingLevelSwitch)
                .AddLoggingTargets(configuration.GetLoggingSettings())
                .ReadFrom.Configuration(configuration)
                .CreateLogger();
    }

    private static void SetLoggingSwitchSwitchLogLevel(IConfiguration configuration)
    {
        var logSettings = configuration.GetLoggingSettings();
        if (logSettings.LogLevel is not null)
            _loggingLevelSwitch.LogLevel = logSettings.LogLevel.Default;
    }

    internal static IHostBuilder ConfigureLogging(this IHostBuilder builder)
    {
        builder.UseSerilog((context, loggerConfiguration) =>
        {
            SetLoggingSwitchSwitchLogLevel(context.Configuration);

            loggerConfiguration
                .OverrideMinimumLevel(context.Configuration, "MassTransit", LogEventLevel.Warning)
                .OverrideMinimumLevel(context.Configuration, "Microsoft", LogEventLevel.Warning)
                .OverrideMinimumLevel(context.Configuration, "Microsoft.Hosting.Lifetime", LogEventLevel.Warning)
                .OverrideMinimumLevel(context.Configuration, "Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .OverrideMinimumLevel(context.Configuration, "Microsoft.AspNetCore.Authentication", LogEventLevel.Warning)
                .OverrideMinimumLevel(context.Configuration, "AspNetCore.HealthChecks", LogEventLevel.Warning)
                .OverrideMinimumLevel(context.Configuration, "System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithModuleId();

            loggerConfiguration
                .AddLoggingTargets(context.Configuration.GetLoggingSettings())
                .ReadFrom.Configuration(context.Configuration)
                .MinimumLevel.ControlledBy(_loggingLevelSwitch.WrappedBaseLoggingLevelSwitch);
        });

        builder.ConfigureServices((_, services) =>
        {
            services.TryAddSingleton<ILogLevelSwitch>(_loggingLevelSwitch);
        });

        return builder;
    }

    private static LoggerConfiguration AddLoggingTargets(this LoggerConfiguration loggerConfiguration,
        LoggingOptions logOptions)
    {
        var logTargets = logOptions.GetLogTargets().ToArray();

        if (logTargets.Contains(LogTarget.Console))
        {
            loggerConfiguration
                .WriteTo.Console(
                    theme: AnsiConsoleTheme.Code,
                    outputTemplate: LogTemplate,
                    formatProvider: CultureInfo.InvariantCulture
                );
        }

        // if suite is deployed via apt the LogTarget.LogFile is disabled by default
        // systemd takes the logs from console and writes it to /var/logs/vicione-suite
        if (logTargets.Contains(LogTarget.LogFile))
        {
            if (string.IsNullOrEmpty(logOptions.LogPath))
                throw new ConfigurationException(nameof(LoggingOptions.LogPath));

            var logPath = logOptions.LogPath;

            loggerConfiguration
                .WriteTo.File(
                    path: Path.Combine(logPath, LogFileNameTemplate),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: LogTemplate,
                    formatProvider: CultureInfo.InvariantCulture
                );
        }

        if (logTargets.Contains(LogTarget.Syslog))
        {
            if (!OperatingSystem.IsLinux())
                throw new InvalidOperationException("Syslog can only be configured for linux");

            loggerConfiguration.WriteTo.LocalSyslog(outputTemplate: SyslogTemplate);
        }

        if (logTargets.Contains(LogTarget.Journal))
        {
            if (!OperatingSystem.IsLinux())
                throw new InvalidOperationException("Journal can only be configured for linux");

            var template = new ExpressionTemplate(JournalLogtemplate);

#pragma warning disable CA2000 // Serilog calls Dispose
            loggerConfiguration.WriteTo.Sink(new JournalSink(null, template, true, true, new()
            {
                Enabled = logOptions.SpamGuard?.Enabled ?? true,
                Window = TimeSpan.FromSeconds(logOptions.SpamGuard?.WindowSizeSeconds ?? 5),
                SummaryCountThreshold = logOptions.SpamGuard?.SummaryCountThreshold ?? 1000,
            }));
#pragma warning restore CA2000
        }

        return loggerConfiguration;
    }

    private static LoggerConfiguration OverrideMinimumLevel(this LoggerConfiguration loggerConfig,
        IConfiguration config,
        string key,
        LogEventLevel defaultLog)
    {
        var logSwitch = new SerilogLogLevelSwitch(new LoggingLevelSwitch(defaultLog));
        var logLevel = config.GetLogLevel(key);
        if (logLevel is not null)
        {
            logSwitch.LogLevel = (LogLevel)logLevel;
        }

        return loggerConfig.MinimumLevel.Override(key, logSwitch.WrappedBaseLoggingLevelSwitch.MinimumLevel);
    }

    private static IEnumerable<LogTarget> GetLogTargets(this ILogOptions logOptions)
    {
        var targetStrings = logOptions.LogTargets;
        if (targetStrings is null || targetStrings.Length == 0)
        {
            yield return LogTarget.Console;
            yield break;
        }

        foreach (var targetString in targetStrings)
        {
            if (Enum.TryParse(targetString, true, out LogTarget targetEnum))
            {
                yield return targetEnum;
            }
        }
    }

    private static LogLevel? GetLogLevel(this IConfiguration configuration, string key)
    {
        var defaultLogLevel = configuration.GetValue<string?>($"{LoggingOptions.ConfigSection}:LogLevel:{key}", null);
        if (defaultLogLevel is not null && Enum.TryParse(defaultLogLevel, true, out LogLevel logLevel))
        {
            return logLevel;
        }

        return null;
    }
}
