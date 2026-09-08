using Core.OS.Hosting;
using Core.OS.Hosting.Contracts;
using Core.OS.Hosting.Services;
using Core.OS.HostManagement.Extensions;
using Core.OS.Instance.Extensions;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Core.OS.Extensions;

internal static partial class WebApplicationBuilderExtensions
{
    public static async Task TryRunCoreOs(this WebApplicationBuilder builder, SuitePreparationContext preparationContext, IPreparationResult preparationResult, string[] args)
    {
        try
        {
            // handle version downgrade case - we trigger a host with minimal api that only serves the downgrade endpoint and then stops the program workflow here.
            if (preparationResult is VersionDowngradePreparationResult downgradeResult)
            {
                await builder.RunDowngradeHost(preparationContext, downgradeResult);
                return;
            }

            // Recovery was already attempted but the suite keeps crashing — stay alive in a terminal error state
            // so the service manager does not trigger another restart loop.
            if (preparationResult is RecoveryExhaustedPreparationResult exhaustedResult)
            {
                await RunExhaustedHost(builder, preparationContext, exhaustedResult, args);
                return;
            }

            // Any other preparation failure (e.g. file system migration, module manifest generation, etc.) — log and abort startup.
            if (preparationResult is IPreparationAbortResult failure)
            {
                LogStartupAborted(preparationContext.Logger, failure.Reason);
                return;
            }

            // AddCoreOs owns the ordering: diagnostics + option validation first, then services.
            // The invalid-options gate below reports any binding/validation failure.
            builder.Services.AddCoreOs(builder.Configuration, preparationContext);

            // all services have to be already registered to service collection!
            var host = builder.Build();

            var failures = host.GetInvalidOptions();
            if (failures is not null)
            {
                await RunInvalidOptionsHost(builder, preparationContext, failures, args);
            }
            else
            {
                await host.RunCoreOs();
            }
        }
        catch (Exception ex)
        {
            LogStartupFailed(preparationContext.Logger, ex);
        }
        finally
        {
            // no part of the host lifecycle disposes the static Serilog logger, so every exit path
            // ends the process with buffered events (e.g. the OpenTelemetry batch) still pending -
            // for a crash-looping edge device the startup error is the one log that must be delivered
            await Log.CloseAndFlushAsync();
        }
    }

    private static async Task RunDowngradeHost(this WebApplicationBuilder builder, SuitePreparationContext preparationContext, VersionDowngradePreparationResult downgradeResult)
    {
        var hostMgmtOptions = builder.Configuration.GetHostManagementOptions();
        var instanceOptions = builder.Configuration.GetInstanceOptions();

        var downgradeOptions = new DowngradeWebApiParameters
        {
            Instance = instanceOptions,
            HostManagement = hostMgmtOptions,
            Logger = preparationContext.Logger,
            DowngradeInformation = downgradeResult.DowngradeInformation
        };

        // run the host that will stop the program workflow here
        await using var downgradeHost = DowngradeWebApiHostBuilder.Build(builder, preparationContext.FileSystem, downgradeOptions);
        await downgradeHost.RunAsync();
    }

    private static async Task RunExhaustedHost(WebApplicationBuilder builder, SuitePreparationContext preparationContext, RecoveryExhaustedPreparationResult exhaustedResult, string[] args)
    {
        var fallbackHost = FallbackHostBuilder.Build(args, new FallbackHostOptions
        {
            Status = FallbackHostStatus.RecoveryExhausted,
            Messages = [exhaustedResult.Reason],
            HttpStatusCode = 503,
            LogLevel = LogLevel.Critical,
            Logger = preparationContext.LoggerFactory.CreateLogger(nameof(FallbackHostBuilder)),
            FileSystem = preparationContext.FileSystem,
            Instance = preparationContext.InstanceOptions,
            HostManagement = builder.Configuration.GetHostManagementOptions(),
        });
        await fallbackHost.RunAsync();
    }

    private static async Task RunInvalidOptionsHost(WebApplicationBuilder builder, SuitePreparationContext preparationContext, IEnumerable<string> failures, string[] args)
    {
        var fallbackHost = FallbackHostBuilder.Build(args, new FallbackHostOptions
        {
            Status = FallbackHostStatus.InvalidOptions,
            Messages = [.. failures],
            HttpStatusCode = 500,
            Logger = preparationContext.LoggerFactory.CreateLogger(nameof(FallbackHostBuilder)),
            FileSystem = preparationContext.FileSystem,
            Instance = preparationContext.InstanceOptions,
            HostManagement = builder.Configuration.GetHostManagementOptions(),
        });
        await fallbackHost.RunAsync();
    }

    [LoggerMessage(LogLevel.Warning, "Suite preparation did not complete: {Reason}. Startup aborted.")]
    private static partial void LogStartupAborted(ILogger logger, string? reason);

    [LoggerMessage(LogLevel.Critical, "Startup Failed")]
    private static partial void LogStartupFailed(ILogger logger, Exception ex);
}
