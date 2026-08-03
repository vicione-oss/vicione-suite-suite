using Core.OS.DbContext;
using Core.OS.MessageBus.Extensions;
using Core.Shared.Instance.Commands;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Core.OS.Instance.Consumers;

/// <summary>
/// Orchestrates a cluster-wide restart. Runs on the master (or standalone) and sends an instance-dependent
/// <see cref="ControlInstance"/> restart command to every registered instance so all nodes restart together
/// and apply pending module changes. Offline nodes receive the command from their durable queue on reconnect.
/// </summary>
public sealed partial class RestartAllInstancesConsumer(IApplicationDbContext applicationDb, ILogger<RestartAllInstancesConsumer> logger) : IConsumer<RestartAllInstances>
{
    public async Task Consume(ConsumeContext<RestartAllInstances> context)
    {
        var correlationId = context.Message.CorrelationId;

        var instanceIds = await applicationDb.InstanceInfo
            .AsNoTracking()
            .Select(i => i.Id)
            .ToListAsync(context.CancellationToken);

        LogDispatch(logger, correlationId, instanceIds.Count);

        foreach (var instanceId in instanceIds)
        {
            var command = new ControlInstance
            {
                InstanceId = instanceId,
                Action = InstanceCommand.Restart,
                Delay = context.Message.Delay,
                CorrelationId = correlationId,
            };

            await context.SendToInstance(command, instanceId, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Dispatching cluster restart correlated by {CorrelationId} to {InstanceCount} instance(s)")]
    private static partial void LogDispatch(ILogger<RestartAllInstancesConsumer> logger, Guid correlationId, int instanceCount);
}
