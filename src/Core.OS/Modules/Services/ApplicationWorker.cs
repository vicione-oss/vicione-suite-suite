using System.Globalization;
using System.IO.Abstractions;
using Core.OS.Connections.Extensions;
using Core.OS.Connections.Mqtt;
using Core.OS.DbContext;
using Core.OS.DbContext.Extensions;
using Core.OS.Instance;
using Core.OS.Instance.Commands;
using Core.OS.Instance.Contracts;
using Core.OS.Logging;
using Core.OS.MessageBus.MassTransit;
using Core.OS.MessageBus.MassTransit.Configuration;
using Core.OS.Modules.Extensions;
using Core.OS.UserManagement.Extensions;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Services;
using Core.Shared.UserManagement.Contracts;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sdk;
using Sdk.Backend.Messaging;

namespace Core.OS.Modules.Services;

internal sealed class ApplicationWorker(IServiceProvider services, ILogger<ApplicationWorker> logger) : IHostedLifecycleService
{
    public string? InitializationErrorMessage { get; private set; }

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting application initialization");

            using var scope = services.CreateScope();//Init Scope
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            MoveResources(scope);

            // migrate our core contexts (application, user etc.)
            await MigrateAndSeedCoreData(scope, cancellationToken);

            await DeleteOrphanedNonces(scope, cancellationToken);

            await SetCulture(scope, cancellationToken);

            InitLocalInstanceInformation(scope);

            // in slave scenario, synchronized with master on second+ start migration SystemModule leads to:
            // Microsoft.Data.Sqlite.SqliteException (0x80004005): SQLite Error 1: 'table "AspNetRoles" already exists'.
            await MigrateAndSeedModuleData(scope, configuration, cancellationToken);
            logger.LogInformation("Migration completed and module data is seeded");

            // clean the rabbit mq before the bus gets started
            await RabbitMqCleaner.CleanVirtualHost(scope.ServiceProvider, logger, cancellationToken);

            // start the message bus in parallel
            var busDepot = scope.ServiceProvider.GetRequiredService<IBusDepot>();
            var task = Task.Run(() => busDepot.Start(cancellationToken), cancellationToken);

            while (task.Status < TaskStatus.Running)
                await Task.Delay(5, cancellationToken);

            // here we have the application db up and running and can register our instance
            await RegisterInstance(scope, cancellationToken);

            var infoProvider = scope.ServiceProvider.GetRequiredService<ILocalInstanceInformationProvider>();
            logger.LogInformation("Init completed as {InstanceType} ({InstanceId})",
                infoProvider.Local.Type,
                infoProvider.Local.Id);
        }
        catch (Exception ex)
        {
            LogInitFailed(ex);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;
        try
        {
            var moduleHost = services.GetRequiredService<IModuleHost>();

            using var scope = services.CreateScope();
            await moduleHost.CallOnInitialized(scope, cancellationToken);

            logger.LogInformation("Modules are initialized");
        }
        catch (Exception ex)
        {
            LogInitFailed(ex);
        }
    }

    public Task StartedAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        var busDepot = services.GetRequiredService<IBusDepot>();
        return busDepot.Stop(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    private void MoveResources(IServiceScope scope)
    {
        var moduleHost = scope.ServiceProvider.GetRequiredService<IModuleHost>();
        moduleHost.MoveModuleResources(services);

        logger.LogInformation("Module resources were moved");
    }

    private async Task DeleteOrphanedNonces(IServiceScope scope, CancellationToken cancellationToken)
    {
        var nonceStore = scope.ServiceProvider.GetRequiredService<INonceStore>();

        try
        {
            await nonceStore.DeletedOrphaned(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // nothing to do here, we simply do a graceful exit
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unexpected error occured while trying to delete orphaned nonces");
        }
    }

    private async Task SetCulture(IServiceScope scope, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        try
        {
            var crossInstanceConfiguration = await context.CrossInstanceConfiguration.FirstOrDefaultAsync(cancellationToken);

            SetCulture(crossInstanceConfiguration?.CultureName ?? CrossInstanceConfiguration.CultureNameDefault);
        }
        catch (OperationCanceledException)
        {
            // nothing to do here, we simply do a graceful exit
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unexpected error occured while trying to set culture");

            SetCulture(CrossInstanceConfiguration.CultureNameDefault);
        }
    }

    private void SetCulture(string cultureName)
    {
        var culture = new CultureInfo(cultureName);

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        logger.LogInformation("Culture set to {CultureName}", cultureName);
    }

    private static async Task MigrateAndSeedCoreData(IServiceScope scope, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        await scope.ServiceProvider.MigrateContext<IApplicationDbContext>(cancellationToken);
        await scope.ServiceProvider.MigrateContext<UserDbContext>(cancellationToken);
        await scope.ServiceProvider.MigrateContext<IConnectionDbContext>(cancellationToken);

        // seed data
        await scope.ServiceProvider.SeedUsersAndRoles();
        await scope.ServiceProvider.SeedConnections(cancellationToken);

        // force regeneration of users security stamps to invalidate current logins
        if (InstanceStartupState.InvalidateLoginsAfterMigration)
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<SuiteUser>>();
            await userManager.InvalidateLogins();
        }
    }

    private static async Task MigrateAndSeedModuleData(IServiceScope scope, IConfiguration config, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        var moduleHost = scope.ServiceProvider.GetRequiredService<IModuleHost>();

        await moduleHost.MigrateAndSeedModuleData(scope, config, cancellationToken);
    }

    private static void InitLocalInstanceInformation(IServiceScope scope)
    {
        var fileSystem = scope.ServiceProvider.GetRequiredService<IFileSystem>();
        var instanceOptions = scope.ServiceProvider.GetRequiredService<IOptions<InstanceOptions>>().Value;
        var moduleHost = scope.ServiceProvider.GetRequiredService<IModuleHost>();
        var localInfoProvider = scope.ServiceProvider.GetRequiredService<ILocalInstanceInformationProvider>();
        var instanceId = localInfoProvider.ReadLocalInstanceId();

        // accessing the dbcontext should happen after RegisterInstanceCommand was consumed
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        // try to get it from application db
        var instanceInfo = dbContext.InstanceInfo.FirstOrDefault(k => k.Id == instanceId);
        if (instanceInfo == null)
        {
            // initialized once
            instanceInfo = new InstanceInformation
            {
                Id = instanceId,
                Type = instanceOptions.Type,
                SerialNumber = instanceOptions.SerialNumber ?? instanceId.ToString("N"),
            };

            // optional
            if (!string.IsNullOrEmpty(instanceOptions.NamePreload))
                instanceInfo.Name = instanceOptions.NamePreload;
            else if (string.IsNullOrEmpty(instanceInfo.Name))
                instanceInfo.Name = Environment.MachineName;

            if (!string.IsNullOrEmpty(instanceOptions.DescriptionPreload))
                instanceInfo.Description = instanceOptions.DescriptionPreload;

            if (!string.IsNullOrEmpty(instanceOptions.FormattedName))
                instanceInfo.FormattedName = instanceOptions.FormattedName;
        }

        // update every startup because installed modules and versions might have changed
        instanceInfo.InstalledModules = moduleHost.GetModules()
            .Select(m => m.ModuleKey.ModuleId)
            .OrderBy(id => id)
            .ToList();

        instanceInfo.Version = fileSystem.EvaluateLocalVersionString(out var branchName);
        instanceInfo.BranchName = branchName;
        instanceInfo.SdkVersion = moduleHost.GetSdkVersion();

        // update local instance info to what we have already because modules might access it
        // on their init process e.g. ClusterManagement on PostMigrate
        localInfoProvider.UpdateLocal(instanceInfo);
    }

    /// <summary>
    /// Send a RegisterInstanceCommand with local instance information available from
    /// <see cref="ILocalInstanceInformationProvider.Local"/> that will update the local
    /// instance on db context
    /// </summary>
    private async Task RegisterInstance(IServiceScope scope, CancellationToken cancellationToken)
    {
        var infoProvider = scope.ServiceProvider.GetRequiredService<ILocalInstanceInformationProvider>();

        logger.LogInformation("Starting registration for instance {InstanceId}", infoProvider.Local.Id);

        var sendEndpointProvider = scope.ServiceProvider.GetRequiredService<ISendEndpointProvider>();
        var config = services.GetRequiredService<IConfiguration>();

        // needs to be sent that way, because the Mediator blocks commands until the Master is reachable
        var endPoint = await sendEndpointProvider.GetSendEndpoint(MessagingHelper.GetCommandEndpointAddress<RegisterInstance>());
        await endPoint.Send(new RegisterInstance
        {
            InstanceId = infoProvider.Local.Id,
            InstalledModules = [.. infoProvider.LoadedModules],
            Type = infoProvider.Local.Type,
            Name = infoProvider.Local.Name,
            Description = infoProvider.Local.Description,
            FormattedName = infoProvider.Local.FormattedName,
            SerialNumber = infoProvider.Local.SerialNumber,
            SdkVersion = infoProvider.Local.SdkVersion,
            Configuration = config.AsEnumerable().Where(IsAllowed).ToList(),
            Version = infoProvider.Local.Version,
            BranchName = infoProvider.Local.BranchName
        },
            cancellationToken);

        logger.LogInformation("Published {Command} on {Type} instance", nameof(Instance.Commands.RegisterInstance), infoProvider.Local.Type);
    }

    private static bool IsAllowed(KeyValuePair<string, string?> keyValuePair)
    {
        // in development there are more than 700 lines of configuration
        // therefore we filter to get reduced subset. 
        string[] knownKeys =
        [
            "AllowedHosts",
            "ASPNETCORE_URLS",
            "COMPUTERNAME",
            "ENVIRONMENT",
            "Kestrel",
            "OS",
            "NUMBER_OF_",
            Constants.ModuleLoaderSection,
            InstanceOptions.ConfigSection,
            MessageBusOptions.ConfigSection,
            MqttClientOptions.ConfigSection,
            LoggingOptions.ConfigSection,
            "PROCESSOR_",
            "PUBLIC",
            "SESSIONNAME",
            "STARTUP_HOOKS",
            "URLS",
            "VSLANG",
        ];

        return knownKeys.Any(k => keyValuePair.Key.StartsWith(k, StringComparison.Ordinal));
    }

    private void LogInitFailed(Exception ex)
    {
        logger.LogCritical(ex, "Init failed");

        InitializationErrorMessage = ex.Message;

        var hostAppService = services.GetRequiredService<IHostApplicationLifetime>();
        hostAppService.StopApplication();
    }
}
