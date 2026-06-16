using System.Diagnostics;
using System.Globalization;
using System.IO.Abstractions;
using Core.Module.Options;
using Core.OS.Connections.Extensions;
using Core.OS.Connections.Mqtt;
using Core.OS.DbContext;
using Core.OS.DbContext.Extensions;
using Core.OS.Diagnostics;
using Core.OS.Instance;
using Core.OS.Instance.Commands;
using Core.OS.Instance.Contracts;
using Core.OS.Instance.Services;
using Core.OS.Logging;
using Core.OS.MessageBus.MassTransit;
using Core.OS.MessageBus.MassTransit.Configuration;
using Core.OS.Modules.Extensions;
using Core.OS.Persistence;
using Core.OS.UserManagement.Extensions;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Services;
using Core.Shared.UserManagement.Contracts;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sdk.Backend.Messaging;
using Sdk.Instance;

namespace Core.OS.Modules.Services;

internal sealed partial class ApplicationWorker(
    IServiceProvider services,
    ILogger<ApplicationWorker> logger) : IHostedLifecycleService
{
    public string? InitializationErrorMessage { get; private set; }

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogStartingApplicationInitialization();

            using var scope = services.CreateScope();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            using var activity = CoreActivitySource.Source.StartActivity();

            // migrate our core contexts (application, user etc.)
            await MigrateCoreData(scope, cancellationToken);
            activity?.AddEvent(new ActivityEvent("app.lifecycle.core_data.migrated"));

            var localInstanceInformationProvider
                = scope.ServiceProvider.GetRequiredService<ILocalInstanceInformationProvider>();
            InitializeLocalInstanceInformation(scope, localInstanceInformationProvider);

            activity?.AddEvent(new ActivityEvent("app.lifecycle.local_instance.initialized"));
            activity?.AddTag("service.instance.id", localInstanceInformationProvider.Local.Id);
            activity?.AddTag("service.instance.type", localInstanceInformationProvider.Local.Type);

            MoveResources(scope);

            if (localInstanceInformationProvider.Local.Type != InstanceType.Slave)
            {
                await SeedInitialData(scope, cancellationToken);
                activity?.AddEvent(new ActivityEvent("app.lifecycle.initial_data.seeded"));
            }

            await DeleteOrphanedNonces(scope.ServiceProvider.GetRequiredService<INonceStore>(), cancellationToken);
            activity?.AddEvent(new ActivityEvent("app.lifecycle.nonces.deleted"));

            // in slave scenario, synchronized with master on second+ start migration SystemModule leads to:
            // Microsoft.Data.Sqlite.SqliteException (0x80004005): SQLite Error 1: 'table "AspNetRoles" already exists'.
            await MigrateAndSeedModuleData(scope, configuration, cancellationToken);
            activity?.AddEvent(new ActivityEvent("app.lifecycle.module_data.seeded"));

            await StartMassTransitBusDependingOnInstanceType(scope,
                localInstanceInformationProvider,
                cancellationToken);

            await WaitForInitialSyncToBeDone(scope);

            await SetCulture(scope, cancellationToken);

            activity?.AddEvent(new ActivityEvent("app.lifecycle.started"));
            logger.LogInitCompleted(localInstanceInformationProvider.Local.Type, localInstanceInformationProvider.Local.Id);
        }
        catch (Exception ex)
        {
            StopApplicationOnFailure(ex);
        }
    }

    private static async Task WaitForInitialSyncToBeDone(IServiceScope scope)
        => await scope.ServiceProvider.GetRequiredService<SynchronizationState>().Ready;

    private async Task StartMassTransitBusDependingOnInstanceType(IServiceScope scope,
        ILocalInstanceInformationProvider localInstanceInformationProvider,
        CancellationToken cancellationToken)
    {
        using var activity
            = CoreActivitySource.Source.StartActivity(
                $"{nameof(ApplicationWorker)}.{nameof(StartMassTransitBusDependingOnInstanceType)}");
        var instanceInformation = localInstanceInformationProvider.Local;
        var loadedModules = localInstanceInformationProvider.LoadedModules;
        var configuration = services.GetRequiredService<IConfiguration>();

        if (instanceInformation.Type == InstanceType.Slave)
        {
            // we send the registration before waiting for the bus to start.
            // this will work because the message is sent directly to the endpoint, not using the bus.
            await RegisterInstance(scope,
                instanceInformation,
                loadedModules,
                configuration,
                cancellationToken);

            activity?.AddEvent(new ActivityEvent("app.lifecycle.register_instance.send"));

            await StartMessageBusDepot(scope, cancellationToken);
        }
        else
        {
            await StartMessageBusDepot(scope, cancellationToken);
            await RegisterInstance(scope,
                instanceInformation,
                loadedModules,
                configuration,
                cancellationToken);

            activity?.AddEvent(new ActivityEvent("app.lifecycle.register_instance.send"));
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        using var activity = CoreActivitySource.Source.StartActivity($"{nameof(ApplicationWorker)}.{nameof(StartAsync)}");

        try
        {
            logger.LogInitializingModuleHost();

            var moduleHost = services.GetRequiredService<IModuleHost>();

            using var scope = services.CreateScope();
            await moduleHost.CallOnInitialized(scope, cancellationToken);
            activity?.AddEvent(new ActivityEvent("app.lifecycle.module_host.initialized"));

            logger.LogModulesInitialized();
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            StopApplicationOnFailure(ex);
        }
    }

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        var busDepot = services.GetRequiredService<IBusDepot>();
        return busDepot.Stop(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task StartMessageBusDepot(IServiceScope scope,
        CancellationToken cancellationToken)
    {
        // clean the rabbit mq before the bus gets started
        await RabbitMqCleaner.CleanVirtualHost(scope.ServiceProvider, logger, cancellationToken);

        // start the message bus in a background task
        var busDepot = scope.ServiceProvider.GetRequiredService<IBusDepot>();
        var task = Task.Run(() => busDepot.Start(cancellationToken), cancellationToken);

        // Wait for it to at least be running/attempting
        while (task is { Status: < TaskStatus.Running, IsFaulted: false })
        {
            await Task.Delay(10, cancellationToken);
        }

        logger.LogMessageBusDepotStarted();
    }

    private void MoveResources(IServiceScope scope)
    {
        logger.LogMovingModuleResources();

        var moduleHost = scope.ServiceProvider.GetRequiredService<IModuleHost>();
        moduleHost.MoveModuleResources(services);

        logger.LogModuleResourcesSynchronized();
    }

    private async Task DeleteOrphanedNonces(INonceStore nonceStore, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogDeletingOrphanedNonces();

            await nonceStore.DeletedOrphaned(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // nothing to do here, we simply do a graceful exit
        }
        catch (Exception e)
        {
            logger.LogErrorDeletingOrphanedNonces(e);
        }
    }

    private async Task SetCulture(IServiceScope scope, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        using var activity = CoreActivitySource.Source.StartActivity();
        try
        {
            var appContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var crossInstanceConfiguration
                = await appContext.CrossInstanceConfiguration.FirstOrDefaultAsync(cancellationToken);

            var cultureName = crossInstanceConfiguration?.CultureName ?? CrossInstanceConfiguration.CultureNameDefault;
            activity?.AddTag("process.runtime.culture", cultureName);
            SetCulture(cultureName);

            var moduleHost = scope.ServiceProvider.GetRequiredService<IModuleHost>();
            moduleHost.SetUiHostCulture(cultureName);
        }
        catch (OperationCanceledException)
        {
            // nothing to do here, we simply do a graceful exit
        }
        catch (Exception e)
        {
            activity?.AddException(e);
            logger.LogErrorSettingCulture(e);

            activity?.AddTag("process.runtime.culture", CrossInstanceConfiguration.CultureNameDefault);
            SetCulture(CrossInstanceConfiguration.CultureNameDefault);
        }
    }

    private void SetCulture(string cultureName)
    {
        var culture = new CultureInfo(cultureName);

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        logger.LogCultureSet(cultureName);
    }

    private async Task MigrateCoreData(IServiceScope scope,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        logger.LogTrace("Migrating application databases");
        await scope.ServiceProvider.MigrateContext<UserDbContext>(cancellationToken);
        await scope.ServiceProvider.MigrateContext<IApplicationDbContext>(cancellationToken);
        await scope.ServiceProvider.MigrateContext<IConnectionDbContext>(cancellationToken);

        // Migrate outbox tables (master only, registered only when Bus Outbox is configured)
        var outboxDbContext = scope.ServiceProvider.GetService<OutboxDbContext>();
        if (outboxDbContext is not null)
        {
            await outboxDbContext.Database.MigrateAsync(cancellationToken);

            // Seed the replication sequence counter from persisted state (ADR-003 Gap 4).
            // Ensures the counter survives master restarts without re-issuing sequence numbers.
            var sequenceCounter = scope.ServiceProvider.GetService<ReplicationSequenceCounter>();
            if (sequenceCounter is not null)
            {
                var states = await outboxDbContext.ReplicationSequenceStates.ToListAsync(cancellationToken);
                foreach (var state in states)
                    sequenceCounter.Seed(state.ContextType, state.LastSequenceNumber);
            }
        }

        // force regeneration of users security stamps to invalidate current logins
        if (InstanceStartupState.InvalidateLoginsAfterMigration)
        {
            logger.LogInvalidatingActiveLogins();

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<SuiteUser>>();
            await userManager.InvalidateLogins();
        }
    }

    private async Task SeedInitialData(IServiceScope scope,
        CancellationToken cancellationToken)
    {
        logger.LogSeedingInitialData();

        await scope.ServiceProvider.SeedUsersAndRoles(cancellationToken);
        await scope.ServiceProvider.SeedConnections(cancellationToken);
    }

    private static async Task MigrateAndSeedModuleData(IServiceScope scope,
        IConfiguration config,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        var moduleHost = scope.ServiceProvider.GetRequiredService<IModuleHost>();

        await moduleHost.MigrateAndSeedModuleData(scope, config, cancellationToken);
    }

    private void InitializeLocalInstanceInformation(IServiceScope scope,
        ILocalInstanceInformationProvider localInfoProvider)
    {
        var fileSystem = scope.ServiceProvider.GetRequiredService<IFileSystem>();
        var instanceOptions = scope.ServiceProvider.GetRequiredService<IOptions<InstanceOptions>>().Value;
        var moduleHost = scope.ServiceProvider.GetRequiredService<IModuleHost>();
        var instanceId = localInfoProvider.ReadLocalInstanceId();

        logger.LogInitializingLocalInstanceInformation();

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
                SerialNumber = instanceOptions.SerialNumber ?? instanceId.ToString("N")
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
        instanceInfo.SystemType = instanceOptions.SystemType ?? "not set";

        // update local instance info to what we have already because modules might access it
        // on their init process e.g. ClusterManagement on PostMigrate
        // Extra State for module process to start?
        localInfoProvider.UpdateLocal(instanceInfo);
    }

    /// <summary>
    /// Send a RegisterInstanceCommand with local instance information available from
    /// <see cref="ILocalInstanceInformationProvider.Local"/> that will update the local
    /// instance on db context
    /// </summary>
    private async Task RegisterInstance(IServiceScope scope,
        IInstanceInformation instanceInfo,
        IReadOnlyCollection<string> loadedModules,
        IConfiguration config,
        CancellationToken cancellationToken)
    {
        logger.LogStartingRegistration(instanceInfo.Id);

        var sendEndpointProvider = scope.ServiceProvider.GetRequiredService<ISendEndpointProvider>();

        // Collect last-applied sequence numbers from the replication tracker (slave only).
        // On a fresh slave or first startup, the tracker is empty → empty dictionary.
        // The master uses this to detect sequence gaps (ADR-003 Gap 4).
        var sequenceTracker = scope.ServiceProvider.GetService<ReplicationSequenceTracker>();
        var lastAppliedSequences = sequenceTracker?.GetAllLastApplied()
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? [];

        // needs to be sent that way, because the bus is blocked to prevent messages being processed before sync is done
        var endPoint = await sendEndpointProvider.GetSendEndpoint(
            MessagingHelper.GetCommandEndpointAddress<RegisterInstance>());
        await endPoint.Send(new RegisterInstance
        {
            InstanceId = instanceInfo.Id,
            InstalledModules = [.. loadedModules],
            Type = instanceInfo.Type,
            Name = instanceInfo.Name,
            Description = instanceInfo.Description,
            FormattedName = instanceInfo.FormattedName,
            SerialNumber = instanceInfo.SerialNumber,
            SystemType = instanceInfo.SystemType,
            SdkVersion = instanceInfo.SdkVersion,
            Configuration = config.AsEnumerable().Where(IsAllowed).ToList(),
            Version = instanceInfo.Version,
            BranchName = instanceInfo.BranchName,
            LastAppliedSequences = lastAppliedSequences
        },
            cancellationToken);

        logger.LogPublishedRegisterInstance(nameof(Instance.Commands.RegisterInstance), instanceInfo.Type);
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
            ModuleLoaderOptions.ConfigSection,
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

    private void StopApplicationOnFailure(Exception ex)
    {
        logger.LogInitFailed(ex);

        InitializationErrorMessage = ex.Message;

        var hostAppService = services.GetRequiredService<IHostApplicationLifetime>();
        hostAppService.StopApplication();
    }
}
