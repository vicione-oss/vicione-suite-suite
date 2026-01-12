using Core.OS.DbContext;
using Core.OS.Instance.Initialization;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Events;
using MassTransit;
using MassTransit.Courier.Contracts;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Extensions;
using Sdk.Backend.Messaging;
using Sdk.Instance;

namespace Core.OS.Instance.Consumers;

public sealed class AdministrateInstanceConsumer : TrackingConsumerBase, IConsumer<AdministrateInstance>
{
    private readonly IServiceProvider _services;
    private readonly IApplicationDbContext _applicationDb;
    private readonly ILogger<AdministrateInstanceConsumer> _logger;

    public AdministrateInstanceConsumer(IServiceProvider services, ILogger<AdministrateInstanceConsumer> logger)
    {
        _services = services;
        _logger = logger;
        _applicationDb = _services.GetRequiredService<IApplicationDbContext>();
    }

    public async Task Consume(ConsumeContext<AdministrateInstance> context)
    {
        _logger.LogInformation("Consume {Command} action:{Action} for instance {InstanceId}",
            nameof(AdministrateInstance),
            context.Message.Action,
            context.Message.InstanceId);

        var localProvider = _services.GetRequiredService<ILocalInstanceInformationProvider>();
        if (localProvider.Local.Type != InstanceType.Master)
            return;

        var targetInstance = await _applicationDb.InstanceInfo.SingleOrDefaultAsync(k => k.Id == context.Message.InstanceId);
        if (targetInstance is null)
        {
            _logger.LogWarning("Instance {InstanceId} is not part of the current cluster", context.Message.InstanceId);
            return;
        }

        switch (context.Message.Action)
        {
            case AdministrateInstanceAction.Synchronize:
                await HandleResyncInstance(context, targetInstance);
                break;

            case AdministrateInstanceAction.Delete:
                await HandleDeleteInstance(context, targetInstance);
                break;
        }
    }

    private async Task HandleResyncInstance(ConsumeContext<AdministrateInstance> context, InstanceInformation instanceInfo)
    {
        if (instanceInfo.Type != InstanceType.Slave)
        {
            _logger.LogWarning("Instance {InstanceId} is not a valid target system for synchronization", context.Message.InstanceId);
            return;
        }

        var factory = _services.GetRequiredService<IRoutingSlipBuilderFactory>();
        var builder = factory.Create(context.CorrelationId ?? Guid.NewGuid());

        var arguments = await SyncDataHelpers.CreateSyncDataArgumentsPg(_services, instanceInfo.InstalledModules, context.CancellationToken);
        foreach (var argument in arguments)
        {
            builder.AddActivity<SyncDataActivity, SyncDataArguments>(argument, instanceInfo.Id);
            _logger.LogDebug("Added sync activity instanceId:{InstanceId} context:{Schema} table:{Table}",
                instanceInfo.Id,
                argument.DbContextTypeName,
                argument.Table);
        }

        builder.AddVariable(SyncDataHelpers.InstanceIdVariableKey, context.Message.InstanceId);
        await ExecuteTracked(context, builder);
    }

    private async Task HandleDeleteInstance(ConsumeContext<AdministrateInstance> context, InstanceInformation instanceInfo)
    {
        var success = false;

        try
        {
            // todo: removing an instance from the system can have impact to the cluster. actions need to be defined

            _applicationDb.InstanceInfo.Remove(instanceInfo);
            await _applicationDb.Instance.SaveChangesAsync(context.CancellationToken);
            success = true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to delete instance {InstanceId}", instanceInfo.Id);
        }

        await context.Publish(new InstanceAdministrated(instanceInfo.Id, AdministrateInstanceAction.Delete, success),
            context.CancellationToken);
    }

    protected override Task ConsumeCompleted(ConsumeContext<RoutingSlipCompleted> context)
    {
        var instanceId = context.Message.GetVariable<Guid>(SyncDataHelpers.InstanceIdVariableKey);
        _logger.LogInformation("Instance {InstanceId} successfully synchronized", instanceId);
        return context.Publish(new InstanceAdministrated(instanceId, AdministrateInstanceAction.Synchronize, true),
            context.CancellationToken);
    }

    protected override Task ConsumeFaulted(ConsumeContext<RoutingSlipFaulted> context)
    {
        var instanceId = context.Message.GetVariable<Guid>(SyncDataHelpers.InstanceIdVariableKey);
        var errorList = context.Message.ActivityExceptions.Select(ex => ex.ExceptionInfo.Message).ToList();
        _logger.LogError(
            "One or more errors occured when synchronizing data for instance {InstanceId}:{NewLine}{ErrorList}",
            instanceId,
            Environment.NewLine,
            string.Join(Environment.NewLine,
                errorList));

        return context.Publish(new InstanceAdministrated(instanceId, AdministrateInstanceAction.Synchronize, false),
            context.CancellationToken);
    }
}
