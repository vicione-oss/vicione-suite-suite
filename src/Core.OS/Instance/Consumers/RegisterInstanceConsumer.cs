using System.Text.Json;
using Core.OS.Connections.Extensions;
using Core.OS.Connections.Mqtt;
using Core.OS.DbContext;
using Core.OS.Instance.Commands;
using Core.OS.Instance.Initialization;
using Core.OS.Instance.Mappers;
using Core.OS.Instance.Services;
using Core.OS.MessageBus.Extensions;
using Core.OS.Modules;
using Core.OS.Modules.Services;
using Core.OS.Persistence;
using Core.Shared.Instance.Contracts;
using Core.Shared.Modules.Commands;
using MassTransit;
using MassTransit.Courier.Contracts;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Extensions;
using Sdk.Backend.Messaging;
using Sdk.Connections.Commands;
using Sdk.Connections.Contracts;
using Sdk.Connections.Extensions;
using Sdk.Instance;
using Sdk.Instance.Events;

namespace Core.OS.Instance.Consumers;

public sealed partial class RegisterInstanceConsumer(IServiceProvider services, ILogger<RegisterInstanceConsumer> logger) :
    TrackingConsumerBase, IConsumer<RegisterInstance>
{
    private readonly IApplicationDbContext _appDb = services.GetRequiredService<IApplicationDbContext>();

    public async Task Consume(ConsumeContext<RegisterInstance> context)
    {
        var correlationId = context.Message.CorrelationId;
        var instanceId = context.Message.InstanceId;

        try
        {
            var isNewInstance =
                !await _appDb.InstanceInfo.AnyAsync(i => i.Id == instanceId, context.CancellationToken);

            LogRegisteringInstance(logger, correlationId, isNewInstance ? "Registering" : "Updating registration of", context.Message.Type, instanceId);

            // Slave synchronization must not touch the db until the routing slip has completed, or it
            // runs into concurrency issues such as modifying rows that do not exist yet. The db updates
            // happen in RoutingSlipCompleted instead.
            if (context.Message.Type == InstanceType.Slave)
            {
                var config = await HandleSlaveInstanceConfiguration(correlationId, instanceId, context.Message.Configuration);
                var factory = services.GetRequiredService<IRoutingSlipBuilderFactory>();
                var builder = factory.Create(context.CorrelationId ?? Guid.NewGuid());

                // The sync activities have to run before anything that changes data.
                await HandleSlaveInstanceSynchronization(builder,
                    instanceId,
                    context.Message.InstalledModules,
                    isNewInstance,
                    context.Message.ForceSync,
                    context.Message.LastAppliedSequences,
                    config,
                    context.CancellationToken);

                // A string variable is omitted when its value is null or empty.
                var commandSerialized = JsonSerializer.Serialize(context.Message, Sdk.Messaging.DefaultJsonSerializerSettings.Default);

                builder.AddVariable(SyncDataHelpers.InstanceCommandKey, commandSerialized);
                builder.AddVariable(SyncDataHelpers.InstanceIsNewVariableKey, isNewInstance);
                builder.AddActivity<SyncDataActivity, SyncDataArguments>(new SyncDataArguments
                {
                    SyncCompleted = true,
                }, instanceId);

                // The routing slip runs when the consume call returns, so the tracked result matters.
                await ExecuteTracked(context, builder);
                return;
            }

            var info = await UpsertInstanceInfo(context.Message, isNewInstance, context.CancellationToken);

            await UpdateInstanceProviders(info, context.CancellationToken);

            services.GetRequiredService<SynchronizationState>().CompleteSynchronization();
        }
        catch (Exception e)
        {
            LogUnexpectedError(logger, e, correlationId, instanceId);
            throw;
        }
    }

    private async Task<InstanceInformation> UpsertInstanceInfo(RegisterInstance command, bool isNewInstance, CancellationToken cancellationToken)
    {
        var existing = await _appDb.InstanceInfo
            .SingleOrDefaultAsync(i => i.Id == command.InstanceId, cancellationToken);

        var registrationTime = DateTimeOffset.UtcNow;
        var branchInfo = command.BranchName is null ? string.Empty : $" (branch: '{command.BranchName}')";

        if (existing is not null)
        {
            command.ApplyTo(existing, registrationTime);

            LogUpdateExistingInstance(logger, existing.Id, existing.Version, branchInfo);

            if (isNewInstance)
                LogUpdateExistingInstanceMarkedNew(logger, existing.Id);

            await _appDb.SaveChangesAsync(cancellationToken);

            return existing;
        }

        var newInfo = command.ToInstanceInformation(registrationTime);

        _appDb.InstanceInfo.Add(newInfo);

        LogAddNewInstance(logger, command.InstanceId, command.Version, branchInfo);

        if (!isNewInstance)
            LogAddNewInstanceMarkedExisting(logger, command.CorrelationId, command.InstanceId);

        await _appDb.SaveChangesAsync(cancellationToken);

        return newInfo;
    }

    private async Task UpdateInstanceProviders(IInstanceInformation localInstanceInformation, CancellationToken cancellationToken)
    {
        services.GetRequiredService<ILocalInstanceInformationProvider>()
            .UpdateLocal(localInstanceInformation);

        await services.GetRequiredService<InMemoryClusterInformationProvider>()
            .Initialize(services.GetRequiredService<ISuiteMediator>(), cancellationToken);
    }

    private async Task HandleSlaveInstanceSynchronization(IRoutingSlipBuilder builder,
        Guid instanceId,
        IEnumerable<string> installedModules,
        bool isNewInstance,
        bool forceSync,
        Dictionary<string, long> lastAppliedSequences,
        IConfiguration configuration,
        CancellationToken cancellation)
    {
        var lastRegistered = (await _appDb.InstanceInfo
            .SingleOrDefaultAsync(i => i.Id == instanceId, cancellation))?.LastRegistered;
        var queueLifetime = configuration.GetMessageBusOptions().QueueLifetimeInDays;

        // Use <= to cover the exact boundary: at exactly QueueLifetimeInDays, RabbitMQ may
        // have already deleted the queue (race with broker GC). Better to resync than miss messages.
        var isOutOfSync = lastRegistered?.AddDays(queueLifetime) <= DateTimeOffset.Now;

        // ADR-003 Gap 4: Detect sequence mismatch caused by master restart or broker data loss.
        // If the slave reports sequences higher than the master's current counter (master restarted
        // with counter reset), or the master's counter is significantly ahead of the slave's
        // reported position (slave missed messages), trigger full-sync.
        var hasSequenceMismatch = DetectSequenceMismatch(lastAppliedSequences);

        if (isNewInstance || isOutOfSync || forceSync || hasSequenceMismatch)
        {
            var arguments = await SyncDataHelpers.CreateSyncDataArgumentsPg(services, installedModules, cancellation);
            foreach (var argument in arguments)
            {
                builder.AddActivity<SyncDataActivity, SyncDataArguments>(argument, instanceId);
                LogAddSyncActivity(logger, instanceId, argument.DbContextTypeName, argument.Table);
            }
        }
    }

    /// <summary>
    ///     Detects sequence number mismatches between a slave's reported last-applied sequences
    ///     and the master's current counter state (ADR-003 Gap 4).
    /// </summary>
    private bool DetectSequenceMismatch(Dictionary<string, long> slaveSequences)
    {
        if (slaveSequences.Count == 0)
            return false; // Fresh slave or first startup — handled by isNewInstance

        var masterCounter = services.GetRequiredService<ReplicationSequenceCounter>();

        foreach (var (contextType, slaveLastApplied) in slaveSequences)
        {
            var masterCurrent = masterCounter.Current(contextType);

            // Slave reports a higher sequence than master knows about → master restarted
            // and its counter wasn't fully restored (defense-in-depth).
            if (slaveLastApplied > masterCurrent)
            {
                LogSequenceMismatchDetected(logger, contextType, slaveLastApplied, masterCurrent, "slave ahead of master");
                return true;
            }

            // "Master ahead of slave" is intentionally not checked here — handled by the
            // queue TTL expiry (isOutOfSync) and the ReplicationSequenceTracker's buffer
            // timeout on the slave side. See ADR-003, Gap 4 sequence exchange table.
        }

        return false;
    }

    private async Task<IConfiguration> HandleSlaveInstanceConfiguration(Guid correlationId, Guid instanceId, List<KeyValuePair<string, string?>> configuration)
    {
        // Master only: stores the slave instance configuration.
        var configRepository = services.GetRequiredService<IInstanceConfigurationRepository>();
        var instanceConfig = await configRepository.StoreConfiguration(instanceId, [.. configuration]);

        LogStoredInstanceConfiguration(logger, correlationId, instanceId);

        return instanceConfig;
    }

    private async Task<List<Connection>> GetChangedInstanceMqttConnections(Guid instanceId, IConfiguration instanceConfig, CancellationToken cancellation)
    {
        var mqttOptions = instanceConfig.GetMqttClientOptions();
        var instanceConnections = new List<Connection>();

        using var scope = services.CreateScope();
        var connectionDbContext = scope.ServiceProvider.GetRequiredService<IConnectionDbContext>();
        var connections = await connectionDbContext.Connections
            .Include(k => k.Tags)
            .AsNoTracking()
            .ToListAsync(cancellation);

        // todo: better check because these are never null because deployment sets the endpoints in deploy.sh
        if (mqttOptions.ServiceClient is not null)
        {
            var connection = SuiteConnectionFactory.CreateDefaultMqttServiceConnection(instanceId, mqttOptions.ServiceClient);
            var changed = GetChangedConnection(connections, connection, MqttConnectionType.TCP, instanceId);
            if (changed is not null)
                instanceConnections.Add(changed);
        }

        if (mqttOptions.WebSocketClient is not null)
        {
            var connection = SuiteConnectionFactory.CreateDefaultMqttWebsocketConnection(instanceId, mqttOptions.WebSocketClient);
            var changed = GetChangedConnection(connections, connection, MqttConnectionType.WebSocket, instanceId);
            if (changed is not null)
                instanceConnections.Add(changed);
        }

        return instanceConnections;
    }

    private Connection? GetChangedConnection(List<Connection> connections, Connection? connection, MqttConnectionType connectionType, Guid instanceId)
    {
        if (connection is null)
            return null;

        var existing = connections.FirstOrDefaultInstanceMqttConnection(instanceId, connectionType);
        if (existing is null)
        {
            LogAddInstanceConnection(logger, instanceId, connection.Name, connection.Id,
                string.Join(", ", connection.Metadata.Select(k => $"{k.Key}:{k.Value}")),
                string.Join(", ", connection.Tags.Select(k => k.Text)));

            return connection;
        }

        // Identical mqtt json and tags mean nothing changed.
        if (!string.IsNullOrEmpty(connection.Json)
            && connection.Json.Equals(existing.Json, StringComparison.Ordinal)
            && connection.Tags.SequenceEqual(existing.Tags))
            return null;

        // The id is kept; everything else is overwritten.
        existing.Assign(connection);

        LogUpdateInstanceConnection(logger, instanceId, connection.Name, connection.Id,
            string.Join(", ", connection.Metadata.Select(k => $"{k.Key}:{k.Value}")),
            string.Join(", ", connection.Tags.Select(k => k.Text)));

        return existing;
    }

    protected override async Task ConsumeCompleted(ConsumeContext<RoutingSlipCompleted> context)
    {
        RegisterInstance? command = null;

        try
        {
            var isNewInstance = context.Message.GetVariable<bool>(SyncDataHelpers.InstanceIsNewVariableKey);
            var serializedCommand = context.Message.GetVariable<string>(SyncDataHelpers.InstanceCommandKey);
            command = DeserializeFromVariables(serializedCommand);

            LogInstanceSynchronized(logger, isNewInstance ? "[NEW]" : string.Empty, command.InstanceId);

            await UpsertInstanceInfo(command, isNewInstance, context.CancellationToken);

            // Master only: stores the slave instance configuration.
            var configRepository = services.GetRequiredService<IInstanceConfigurationRepository>();
            var config = await configRepository.GetConfiguration(command.InstanceId);

            if (config is not null)
            {
                var instanceConnections = await GetChangedInstanceMqttConnections(command.InstanceId, config, context.CancellationToken);
                foreach (var connection in instanceConnections)
                    await context.Send(MessagingHelper.GetCommandEndpointAddress<UpsertConnection>(), new UpsertConnection(connection));
            }

            LogInstanceFullySynchronized(logger, command.InstanceId);

            await context.Publish(new InstanceCreated(command.InstanceId),
                context.CancellationToken);

            await SendModuleManifestReconciliation(context, command.InstanceId, command.CorrelationId, context.CancellationToken);
        }
        catch (Exception e)
        {
            await context.Publish(new InstanceSynchronizationFailed(command?.InstanceId ?? Guid.Empty,
                    [
                        e.Message
                    ]),
                context.CancellationToken);
        }
    }

    internal async Task SendModuleManifestReconciliation(ConsumeContext context, Guid instanceId, Guid correlationId, CancellationToken cancellationToken)
    {
        try
        {
            var manifestStore = services.GetRequiredService<IModulePackageManifestStore>();
            var operationStore = services.GetRequiredService<IModulePackageOperationStore>();

            var manifest = await manifestStore.Load(cancellationToken);
            var pendingOperations = await operationStore.GetEnqueuedOperations(cancellationToken);

            // Desired set = the master's applied manifest with its own still-pending operations applied. This
            // covers a node joining while a cluster-wide update the master has not yet restarted for is in flight.
            var desiredPackages = ModulePackageOperationProcessor.DeterminePackageChanges(manifest, pendingOperations);

            var reconcile = new ReconcileModuleManifest(desiredPackages)
            {
                InstanceId = instanceId,
                CorrelationId = correlationId,
            };

            await context.SendToInstance(reconcile, instanceId, cancellationToken);

            LogSendReconcile(logger, instanceId, desiredPackages.Count);
        }
        catch (Exception e)
        {
            // Best-effort convergence: a failure here must not fail the registration/sync.
            LogReconcileSendFailed(logger, e, instanceId);
        }
    }

    protected override Task ConsumeFaulted(ConsumeContext<RoutingSlipFaulted> context)
    {
        var serializedCommand = context.Message.GetVariable<string>(SyncDataHelpers.InstanceCommandKey);
        var command = DeserializeFromVariables(serializedCommand);
        var errorList = context.Message.ActivityExceptions.Select(ex => ex.ExceptionInfo.Message).ToList();

        LogInstanceSynchronizationFailed(logger, command.InstanceId, Environment.NewLine, string.Join(Environment.NewLine, errorList));
        return context.Publish(new InstanceSynchronizationFailed(command.InstanceId, errorList),
            context.CancellationToken);
    }

    private static RegisterInstance DeserializeFromVariables(string serializedCommand) =>
        JsonSerializer.Deserialize<RegisterInstance>(
            serializedCommand, Sdk.Messaging.DefaultJsonSerializerSettings.Default) ??
            throw new InvalidOperationException("Failed to deserialize instance command");


    [LoggerMessage(Level = LogLevel.Information, Message = "{Action} instance {InstanceType} {Instance} correlated by {CorrelationId}")]
    private static partial void LogRegisteringInstance(ILogger<RegisterInstanceConsumer> logger, Guid correlationId, string action, InstanceType instanceType, Guid instance);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to register instance {Instance} correlated by {CorrelationId}")]
    private static partial void LogUnexpectedError(ILogger<RegisterInstanceConsumer> logger, Exception exception, Guid correlationId, Guid instance);

    [LoggerMessage(Level = LogLevel.Information, Message = "Update existing instance {InstanceId} with version '{Version}'{BranchInfo}")]
    private static partial void LogUpdateExistingInstance(ILogger<RegisterInstanceConsumer> logger, Guid instanceId, string? version, string branchInfo);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Update existing instance {InstanceId} but it is marked as new")]
    private static partial void LogUpdateExistingInstanceMarkedNew(ILogger<RegisterInstanceConsumer> logger, Guid instanceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Add existing instance {InstanceId} with version '{Version}'{BranchInfo}")]
    private static partial void LogAddNewInstance(ILogger<RegisterInstanceConsumer> logger, Guid instanceId, string? version, string branchInfo);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Added new instance {InstanceId} but it is marked as existing correlated by {CorrelationId}")]
    private static partial void LogAddNewInstanceMarkedExisting(ILogger<RegisterInstanceConsumer> logger, Guid correlationId, Guid instanceId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Added sync activity instanceId:{InstanceId} context:{Schema} table:{Table}")]
    private static partial void LogAddSyncActivity(ILogger<RegisterInstanceConsumer> logger, Guid instanceId, string? schema, string? table);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stored configuration of instance {InstanceId} correlated by {CorrelationId}")]
    private static partial void LogStoredInstanceConfiguration(ILogger<RegisterInstanceConsumer> logger, Guid correlationId, Guid instanceId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Add instance {InstanceId} connection {Name} Id:{ConnectionId} Meta:{MetaData} Tags:{Tags}")]
    private static partial void LogAddInstanceConnection(ILogger<RegisterInstanceConsumer> logger, Guid instanceId, string? name, Guid connectionId, string metaData, string tags);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Update instance {InstanceId} connection {Name} Id:{ConnectionId} Meta:{MetaData} Tags:{Tags}")]
    private static partial void LogUpdateInstanceConnection(ILogger<RegisterInstanceConsumer> logger, Guid instanceId, string? name, Guid connectionId, string metaData, string tags);

    [LoggerMessage(Level = LogLevel.Information, Message = "Instance{newInstance} '{Instance}' successfully synchronized")]
    private static partial void LogInstanceSynchronized(ILogger<RegisterInstanceConsumer> logger, string newInstance, Guid instance);

    [LoggerMessage(Level = LogLevel.Information, Message = "Instance '{Instance}' fully synchronized")]
    private static partial void LogInstanceFullySynchronized(ILogger<RegisterInstanceConsumer> logger, Guid instance);

    [LoggerMessage(Level = LogLevel.Error, Message = "One or more errors occurred when synchronizing data for instance {InstanceId}:{NewLine}{ErrorList}")]
    private static partial void LogInstanceSynchronizationFailed(ILogger<RegisterInstanceConsumer> logger, Guid instanceId, string newLine, string errorList);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sequence mismatch detected for context '{ContextType}': slave reports {SlaveSequence}, master has {MasterSequence} ({Reason}). Triggering full-sync.")]
    private static partial void LogSequenceMismatchDetected(ILogger<RegisterInstanceConsumer> logger, string contextType, long slaveSequence, long masterSequence, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sent module manifest reconciliation to instance {InstanceId} with {PackageCount} desired package(s)")]
    private static partial void LogSendReconcile(ILogger<RegisterInstanceConsumer> logger, Guid instanceId, int packageCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to send module manifest reconciliation to instance {InstanceId}")]
    private static partial void LogReconcileSendFailed(ILogger<RegisterInstanceConsumer> logger, Exception exception, Guid instanceId);
}
