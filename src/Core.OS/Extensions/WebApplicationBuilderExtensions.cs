using System.IO.Abstractions;
using Core.OS.Hosting;
using Core.OS.Hosting.Contracts;
using Core.OS.Hosting.Extensions;
using Core.OS.Hosting.Services;
using Core.OS.HostManagement.Extensions;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.Logging;
using Serilog;

namespace Core.OS.Extensions;

internal static partial class WebApplicationBuilderExtensions
{
    public static async Task<IPreparationResult> PrepareSuite(this WebApplicationBuilder builder, IFileSystem fileSystem, InstanceOptions instanceOptions, Serilog.ILogger logger, CancellationToken cancellationToken = default)
    {
        var preparation = new SuitePreparationPipeline()
        .UseInstanceId(fileSystem, instanceOptions)
        .UseDeviceImageCleanup(fileSystem, instanceOptions, logger)
        .UseResetFile(fileSystem, instanceOptions, logger)
        .UseRestore(fileSystem, instanceOptions, logger)
        .UseVersionDowngradeCheck(fileSystem, instanceOptions, logger)
        .UseRecoveryMode(builder, fileSystem, instanceOptions, logger);

        return await preparation.RunAsync(cancellationToken);
    }

    public static async Task TryRunCoreOs(this WebApplicationBuilder builder, IFileSystem fileSystem, IPreparationResult preparationResult, string[] args)
    {
        try
        {
            // handle version downgrade case - we trigger a host with minimal api that only serves the downgrade endpoint and then stops the program workflow here.
            if (preparationResult is VersionDowngradePreparationResult downgradeResult)
            {
                var hostMgmtOptions = builder.Configuration.GetHostManagementOptions();
                var instanceOptions = builder.Configuration.GetInstanceOptions();

                var downgradeOptions = new DowngradeWebApiParameters
                {
                    Instance = instanceOptions,
                    HostManagement = hostMgmtOptions,
                    Logger = Log.Logger,
                    DowngradeInformation = downgradeResult.DowngradeInformation
                };

                // run the host that will stop the program workflow here
                await using var downgradeHost = DowngradeWebApiHostBuilder.Build(builder, fileSystem, downgradeOptions);
                await downgradeHost.RunAsync();
                return;
            }

            if (preparationResult is IPreparationAbortResult failure)
            {
                Log.Warning("Suite preparation did not complete: {Reason}. Startup aborted.", failure.Reason);
                return;
            }

            // all services have to be already registered to service collection!
            var host = builder.Build();

            var failures = host.GetInvalidOptions();
            if (failures is not null)
            {
                var fallbackHost = InvalidOptionsHostBuilder.Build(args, [.. failures]);
                await fallbackHost.RunAsync();
            }
            else
            {
                await host.RunCoreOs();
            }
        }
        catch (Exception ex)
        {
            LoggingConfiguration.SetupStaticStartupLogger(builder.Configuration);
            Log.Error(ex, "Startup Failed");
        }
    }
}
