using System.Text.Json;
using Core.OS.Connections.Extensions;
using Core.OS.Connections.Mqtt;
using Core.OS.DbContext;
using Core.OS.Instance.Commands;
using Core.OS.Instance.Contracts;
using Core.OS.Instance.Initialization;
using Core.OS.Instance.Services;
using Core.OS.MessageBus.Extensions;
using Core.OS.Persistence;
using Core.Shared.Instance.Contracts;
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

public sealed class RegisterInstanceConsumer(IServiceProvider services, ILogger<RegisterInstanceConsumer> logger) :
    TrackingConsumerBase, IConsumer<RegisterInstance>
{
    private readonly IApplicationDbContext _appDb = services.GetRequiredService<IApplicationDbContext>();

    public async Task Consume(ConsumeContext<RegisterInstance> context)
    {
        try
        {
            var isNewInstance =
                !await _appDb.InstanceInfo.AnyAsync(i => i.Id == context.Message.InstanceId, context.CancellationToken);

            logger.LogInformation("{Action} instance {InstanceType} {Instance}",
                isNewInstance ? "Registering" : "Updating registration of",
                context.Message.Type,
                context.Message.InstanceId);

            // Attention: for slave synchronisation we have to ensure that db changes are only triggered after routing slip has completed
            // otherwise we'll run into concurrency issues (modify not existing rows etc.)
            // In this case the db updates will be done on RoutingSlipCompleted
            if (context.Message.Type == InstanceType.Slave)
            {
                var config = await HandleSlaveInstanceConfiguration(context.Message.InstanceId, context.Message.Configuration);
                var factory = services.GetRequiredService<IRoutingSlipBuilderFactory>();
                var builder = factory.Create(context.CorrelationId ?? Guid.NewGuid());

                // add the sync activities before any other data changing activity
                await HandleSlaveInstanceSynchronization(builder,
                    context.Message.InstanceId,
                    context.Message.InstalledModules,
                    isNewInstance,
                    config,
                    context.CancellationToken);

                // e.g. string variable won't be added if its value is null or empty
                var commandSerialized = JsonSerializer.Serialize(context.Message, Sdk.Messaging.DefaultJsonSerializerSettings.Default);

                builder.AddVariable(SyncDataHelpers.InstanceCommandKey, commandSerialized);
                builder.AddVariable(SyncDataHelpers.InstanceIsNewVariableKey, isNewInstance);
                builder.AddActivity<SyncDataActivity, SyncDataArguments>(new SyncDataArguments
                {
                    SyncCompleted = true,
                },
                    context.Message.InstanceId);

                // routing slip gets executed on leaving the consume call -> handle tracked result!
                await ExecuteTracked(context, builder);
                return;
            }

            // update master information
            var info = await UpsertInstanceInfo(context.Message, isNewInstance, context.CancellationToken);

            // update local instance info caches
            await UpdateInstanceProviders(info, context.CancellationToken);

            // trigger synchronization state completion
            services.GetRequiredService<SynchronizationState>().CompleteSynchronization();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to register instance {Instance}", context.Message.InstanceId);
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
            existing.InstalledModules = command.InstalledModules;
            existing.Type = command.Type;
            existing.Name = command.Name;
            if (command.FormattedName is not null)
                existing.FormattedName = command.FormattedName;
            existing.Description = command.Description;
            existing.SerialNumber = command.SerialNumber;
            existing.SdkVersion = command.SdkVersion;
            existing.LastRegistered = registrationTime;
            existing.Version = command.Version;
            existing.BranchName = command.BranchName;

            logger.LogInformation("Update existing instance {InstanceId} with version '{Version}'{BranchInfo}", existing.Id, existing.Version, branchInfo);

            if (isNewInstance)
                logger.LogWarning("Update existing instance {InstanceId} but it is marked as new", existing.Id);

            await _appDb.Instance.SaveChangesAsync(cancellationToken);

            return existing;
        }

        var newInfo = new InstanceInformation
        {
            Id = command.InstanceId,
            Type = command.Type,
            InstalledModules = command.InstalledModules,
            Name = command.Name,
            Description = command.Description,
            SerialNumber = command.SerialNumber,
            SdkVersion = command.SdkVersion,
            FirstTimeRegistered = registrationTime,
            LastRegistered = registrationTime,
            Version = command.Version,
            BranchName = command.BranchName
        };
        if (command.FormattedName is not null)
            newInfo.FormattedName = command.FormattedName;

        _appDb.InstanceInfo.Add(newInfo);

        logger.LogInformation("Add existing instance {InstanceId} with version '{Version}'{BranchInfo}", command.InstanceId, command.Version, branchInfo);

        if (!isNewInstance)
            logger.LogWarning("Added new instance {InstanceId} but it is marked as existing", command.InstanceId);

        await _appDb.Instance.SaveChangesAsync(cancellationToken);

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
        IConfiguration configuration,
        CancellationToken cancellation)
    {
        var lastRegistered = (await _appDb.InstanceInfo
            .SingleOrDefaultAsync(i => i.Id == instanceId, cancellation))?.LastRegistered;
        var queueLifetime = configuration.GetMessageBusOptions().QueueLifetimeInDays;

        var isOutOfSync = lastRegistered?.AddDays(queueLifetime) < DateTimeOffset.Now;

        if (isNewInstance || isOutOfSync)
        {
            var arguments = await SyncDataHelpers.CreateSyncDataArgumentsPg(services, installedModules, cancellation);
            foreach (var argument in arguments)
            {
                builder.AddActivity<SyncDataActivity, SyncDataArguments>(argument, instanceId);
                logger.LogDebug("Added sync activity instanceId:{InstanceId} context:{Schema} table:{Table}",
                    instanceId,
                    argument.DbContextTypeName,
                    argument.Table);
            }
        }
    }

    private async Task<IConfiguration> HandleSlaveInstanceConfiguration(Guid instanceId, List<KeyValuePair<string, string?>> configuration)
    {
        // only master can access this one - stores the slave instance config
        var configRepository = services.GetRequiredService<IInstanceConfigurationRepository>();
        var instanceConfig = await configRepository.StoreConfiguration(instanceId, [.. configuration]);

        logger.LogInformation("Stored configuration of instance {InstanceId}", instanceId);

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
            logger.LogDebug("Add instance {InstanceId} connection {Name} Id:{ConnectionId} Meta:{MetaData} Tags:{Tags}",
                instanceId,
                connection.Name,
                connection.Id,
                string.Join(", ", connection.Metadata.Select(k => $"{k.Key}:{k.Value}")),
                string.Join(", ", connection.Tags.Select(k => k.Text)));

            return connection;
        }

        // if the mqtt json and tags mat
        if (!string.IsNullOrEmpty(connection.Json)
            && connection.Json.Equals(existing.Json, StringComparison.Ordinal)
            && connection.Tags.SequenceEqual(existing.Tags))
            return null;

        // keep the id and update the rest
        existing.Assign(connection);

        logger.LogDebug("Update instance {InstanceId} connection {Name} Id:{ConnectionId} Meta:{MetaData} Tags:{Tags}",
            instanceId,
            connection.Name,
            connection.Id,
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

            logger.LogInformation("Instance{New} '{Instance}' successfully synchronized",
                isNewInstance ? "[NEW]" : string.Empty,
                command.InstanceId);

            await UpsertInstanceInfo(command, isNewInstance, context.CancellationToken);

            // only master can access this one - stores the slave instance config
            var configRepository = services.GetRequiredService<IInstanceConfigurationRepository>();
            var config = await configRepository.GetConfiguration(command.InstanceId);

            if (config is not null)
            {
                var instanceConnections = await GetChangedInstanceMqttConnections(command.InstanceId, config, context.CancellationToken);
                foreach (var connection in instanceConnections)
                    await context.Send(MessagingHelper.GetCommandEndpointAddress<UpsertConnection>(), new UpsertConnection(connection));
            }

            logger.LogInformation("Instance '{Instance}' successfully synchronized", command.InstanceId);

            await context.Publish(new InstanceCreated(command.InstanceId),
                context.CancellationToken);
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

    protected override Task ConsumeFaulted(ConsumeContext<RoutingSlipFaulted> context)
    {
        var serializedCommand = context.Message.GetVariable<string>(SyncDataHelpers.InstanceCommandKey);
        var command = DeserializeFromVariables(serializedCommand);
        var errorList = context.Message.ActivityExceptions.Select(ex => ex.ExceptionInfo.Message).ToList();

        logger.LogError(
            "One or more errors occured when synchronizing data for instance {InstanceId}:{NewLine}{ErrorList}",
            command.InstanceId,
            Environment.NewLine,
            string.Join(Environment.NewLine,
                errorList));
        return context.Publish(new InstanceSynchronizationFailed(command.InstanceId, errorList),
            context.CancellationToken);
    }

    private static RegisterInstance DeserializeFromVariables(string serializedCommand) =>
        JsonSerializer.Deserialize<RegisterInstance>(
            serializedCommand, Sdk.Messaging.DefaultJsonSerializerSettings.Default) ??
            throw new InvalidOperationException("Failed to deserialize instance command");
}
