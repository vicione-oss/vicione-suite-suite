using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Abstractions;
using Core.OS.Diagnostics;
using Core.OS.Diagnostics.Extensions;
using Core.OS.Instance;
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

    private const string JournalLogtemplate =
        "{#if SourceContext is not null}[{SourceContext}]{#if ModuleId is not null}({ModuleId}){#end} {#end}{@m}{#if @x is not null}\n{@x}{#end}";

    private const string GrpcProtocolName = "grpc";
    private const string HttpProtobufProtocolName = "http/protobuf";

    private const int MinimumQueueLimit = 1;

    private const string QueueLimitConfigurationKey =
        $"{LoggingOptions.ConfigSection}:{LoggingOpenTelemetryOptions.ConfigSection}:{nameof(LoggingOpenTelemetryOptions.QueueLimit)}";

    private static readonly SerilogLogLevelSwitch _loggingLevelSwitch
        = new(new LoggingLevelSwitch(LogEventLevel.Warning));

    public static LoggingOptions GetLoggingSettings(this IConfiguration config)
        => config.GetSection(LoggingOptions.ConfigSection).Get<LoggingOptions>() ?? throw new ConfigurationException(LoggingOptions.ConfigSection);

    /// <summary>
    /// creates the logger used on startup methods
    /// https://stackoverflow.com/questions/66045967/serilog-static-logger-is-silentlogger-when-theres-an-exception-in-masstransit-a
    /// </summary>
    internal static void SetupStaticStartupLogger(IConfiguration configuration,
        IFileSystem fileSystem,
        InstanceOptions instanceOptions)
    {
        SetLoggingSwitchSwitchLogLevel(configuration);

        var loggerConfiguration = new LoggerConfiguration();
        var openTelemetrySinkError = loggerConfiguration.ApplyConfiguration(configuration, fileSystem, instanceOptions);
        Log.Logger = loggerConfiguration.CreateLogger();

        WarnOnOpenTelemetryMisconfiguration(configuration, openTelemetrySinkError);
    }

    private static void WarnOnOpenTelemetryMisconfiguration(IConfiguration configuration, string? openTelemetrySinkError)
    {
        var logOptions = configuration.GetLoggingSettings();
        if (!logOptions.GetLogTargets().Contains(LogTarget.OpenTelemetry))
            return;

        var exporterOptions = configuration.GetOtelExporterOptions();

        if (string.IsNullOrWhiteSpace(exporterOptions.Endpoint))
            Log.Warning(
                "Log target {LogTarget} is configured but {EnvironmentVariable} is not set - OpenTelemetry logging is disabled",
                LogTarget.OpenTelemetry,
                OtelEnvironment.Endpoint);
        else if (!IsValidOpenTelemetryEndpoint(exporterOptions.Endpoint))
            Log.Warning(
                "Log target {LogTarget} is configured but {EnvironmentVariable} value {Endpoint} is not an absolute http(s) URL - OpenTelemetry logging is disabled",
                LogTarget.OpenTelemetry,
                OtelEnvironment.Endpoint,
                exporterOptions.Endpoint);

        // The sink matches protocol names case-sensitively, so this check must too.
        var protocol = exporterOptions.Protocol;
        if (!string.IsNullOrEmpty(protocol)
            && !string.Equals(protocol, GrpcProtocolName, StringComparison.Ordinal)
            && !string.Equals(protocol, HttpProtobufProtocolName, StringComparison.Ordinal))
            Log.Warning(
                "{EnvironmentVariable} value {Protocol} is not supported - OpenTelemetry logging falls back to gRPC",
                OtelEnvironment.Protocol,
                protocol);

        if (logOptions.OpenTelemetry is { QueueLimit: < MinimumQueueLimit } sinkOptions)
            Log.Warning(
                "{ConfigurationKey} value {QueueLimit} is not positive - OpenTelemetry logging falls back to {Fallback}",
                QueueLimitConfigurationKey,
                sinkOptions.QueueLimit,
                MinimumQueueLimit);

        if (openTelemetrySinkError is not null)
            Log.Warning(
                "The OpenTelemetry sink rejected the OTLP configuration: {Reason} - OpenTelemetry logging is disabled",
                openTelemetrySinkError);
    }

    private static void SetLoggingSwitchSwitchLogLevel(IConfiguration configuration)
    {
        var logSettings = configuration.GetLoggingSettings();
        if (logSettings.LogLevel is not null)
            _loggingLevelSwitch.LogLevel = logSettings.LogLevel.Default;
    }

    internal static IServiceCollection ConfigureLogging(this IServiceCollection services,
        IConfiguration configuration,
        IFileSystem fileSystem,
        InstanceOptions instanceOptions)
    {
        // Used to log during startup, before the service provider is ready.
        SetupStaticStartupLogger(configuration, fileSystem, instanceOptions);

        SetLoggingSwitchSwitchLogLevel(configuration);

        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders(); // clear default providers, otherwise we would have double entries in console
            loggingBuilder.AddSerilog(Log.Logger, dispose: true);
        });

        services.TryAddSingleton<ILogLevelSwitch>(_loggingLevelSwitch);

        return services;
    }

    /// <summary>
    /// Returns the reason the OpenTelemetry sink rejected the OTLP configuration, or
    /// null when it was accepted or not configured at all. It cannot be logged here:
    /// the logger is only created once this method returns.
    /// </summary>
    private static string? ApplyConfiguration(this LoggerConfiguration loggerConfiguration,
        IConfiguration configuration,
        IFileSystem fileSystem,
        InstanceOptions instanceOptions)
    {
        loggerConfiguration
                .OverrideMinimumLevel(configuration, "MassTransit", LogEventLevel.Warning)
                .OverrideMinimumLevel(configuration, "Microsoft", LogEventLevel.Warning)
                .OverrideMinimumLevel(configuration, "Microsoft.Hosting.Lifetime", LogEventLevel.Warning)
                .OverrideMinimumLevel(configuration, "Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .OverrideMinimumLevel(configuration, "Microsoft.AspNetCore.Authentication", LogEventLevel.Warning)
                .OverrideMinimumLevel(configuration, "AspNetCore.HealthChecks", LogEventLevel.Warning)
                .OverrideMinimumLevel(configuration, "System", LogEventLevel.Warning)
                .OverrideMinimumLevel(configuration, "ViciOne.Ui.MonochromeIcons.Assets", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithModuleId();

        var openTelemetrySinkError = loggerConfiguration
            .AddLoggingTargets(configuration.GetLoggingSettings(), configuration, fileSystem, instanceOptions);

        loggerConfiguration
            .ReadFrom.Configuration(configuration)
            .MinimumLevel.ControlledBy(_loggingLevelSwitch.WrappedBaseLoggingLevelSwitch);

        return openTelemetrySinkError;
    }

    private static string? AddLoggingTargets(this LoggerConfiguration loggerConfiguration,
        LoggingOptions logOptions,
        IConfiguration configuration,
        IFileSystem fileSystem,
        InstanceOptions instanceOptions)
    {
        var logTargets = logOptions.GetLogTargets().ToArray();

        if (logTargets.Contains(LogTarget.Console))
            loggerConfiguration.AddConsoleSink();

        if (logTargets.Contains(LogTarget.LogFile))
            loggerConfiguration.AddFileSink(logOptions);

        if (logTargets.Contains(LogTarget.Journal))
            loggerConfiguration.AddJournalSink(logOptions);

        return logTargets.Contains(LogTarget.OpenTelemetry)
            ? loggerConfiguration.AddOpenTelemetrySink(configuration, logOptions, fileSystem, instanceOptions)
            : null;
    }

    private static void AddConsoleSink(this LoggerConfiguration loggerConfiguration)
        => loggerConfiguration
            .WriteTo.Console(
                theme: AnsiConsoleTheme.Code,
                outputTemplate: LogTemplate,
                formatProvider: CultureInfo.InvariantCulture
            );

    // A suite deployed via apt has LogTarget.LogFile disabled by default;
    // systemd takes the console logs and writes them to /var/logs/vicione-suite.
    private static void AddFileSink(this LoggerConfiguration loggerConfiguration, LoggingOptions logOptions)
    {
        if (string.IsNullOrEmpty(logOptions.LogPath))
            throw new ConfigurationException(nameof(LoggingOptions.LogPath));

        loggerConfiguration
            .WriteTo.File(
                path: Path.Combine(logOptions.LogPath, LogFileNameTemplate),
                rollingInterval: RollingInterval.Day,
                outputTemplate: LogTemplate,
                formatProvider: CultureInfo.InvariantCulture
            );
    }

    private static void AddJournalSink(this LoggerConfiguration loggerConfiguration, LoggingOptions logOptions)
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

    /// <summary>
    /// A misconfigured endpoint must degrade to a startup warning instead of an
    /// exception: this code runs before any logger exists, so throwing here puts
    /// an edge device into an undiagnosable restart loop.
    /// </summary>
    private static bool IsValidOpenTelemetryEndpoint([NotNullWhen(true)] string? endpoint)
        => Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// Returns the reason the sink rejected the OTLP configuration, or null when it was accepted.
    /// </summary>
    private static string? AddOpenTelemetrySink(this LoggerConfiguration loggerConfiguration,
        IConfiguration configuration,
        LoggingOptions logOptions,
        IFileSystem fileSystem,
        InstanceOptions instanceOptions)
    {
        var exporterOptions = configuration.GetOtelExporterOptions();
        if (!IsValidOpenTelemetryEndpoint(exporterOptions.Endpoint))
            return null;

        var sinkOptions = logOptions.OpenTelemetry ?? new LoggingOpenTelemetryOptions();

        try
        {
            // The sink applies the standard OTLP variables (OTEL_EXPORTER_OTLP_*,
            // OTEL_RESOURCE_ATTRIBUTES, OTEL_SERVICE_NAME) itself with spec-compliant
            // parsing; they are looked up through IConfiguration instead of the raw
            // process environment, so values from other configuration providers
            // (e.g., instance configuration distributed by the master) are honored too.
            loggerConfiguration.WriteTo.OpenTelemetry(options =>
            {
                options.ResourceAttributes = SuiteOtelResource.GetResourceAttributes(exporterOptions, fileSystem, instanceOptions);
                options.RestrictedToMinimumLevel = SerilogLogLevelSwitch.ToLogEventLevel(sinkOptions.MinimumLevel);
                // The logger is created before options validation runs, so an invalid
                // configured limit must not be able to fail logger creation
                options.BatchingOptions.QueueLimit = Math.Max(MinimumQueueLimit, sinkOptions.QueueLimit);
            }, configuration.GetValue<string?>);

            return null;
        }
        catch (Exception ex)
        {
            // The sink rejects malformed OTLP variables (headers, resource attributes,
            // endpoint) with an exception at configuration time; the suite must still
            // start with its remaining log targets instead of crash-looping, and no
            // logger exists yet, so the reason is reported after logger creation
            return ex.Message;
        }
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
