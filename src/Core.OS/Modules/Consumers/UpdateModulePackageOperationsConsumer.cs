using Core.OS.DbContext;
using Core.OS.MessageBus.Extensions;
using Core.Shared.Modules.Commands;
using Core.Shared.Modules.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sdk.Messaging;

namespace Core.OS.Modules.Consumers;

/// <summary>
/// Orchestrates a cluster-wide module package update. Runs on the master (or standalone) and fans out an
/// instance-dependent <see cref="EnqueueModulePackageOperations"/> command to every known instance so each
/// node enqueues the same operations locally and applies them on its next restart. Offline nodes receive
/// the command from their durable queue once they reconnect.
/// </summary>
public sealed partial class UpdateModulePackageOperationsConsumer(IApplicationDbContext applicationDb, ILogger<UpdateModulePackageOperationsConsumer> logger) : IConsumer<UpdateModulePackageOperations>
{
    public async Task Consume(ConsumeContext<UpdateModulePackageOperations> context)
    {
        var correlationId = context.Message.CorrelationId;
        var operations = context.Message.Operations;

        try
        {
            var instanceIds = await applicationDb.InstanceInfo
                .AsNoTracking()
                .Select(i => i.Id)
                .ToListAsync(context.CancellationToken);

            LogDispatch(logger, correlationId, operations.Count, instanceIds.Count);

            foreach (var instanceId in instanceIds)
            {
                var command = new EnqueueModulePackageOperations(operations)
                {
                    InstanceId = instanceId,
                    CorrelationId = correlationId
                };

                await context.SendToInstance(command, instanceId, context.CancellationToken);
            }
        }
        catch (Exception ex)
        {
            LogError(logger, ex, operations.Count);

            var error = new ErrorInfo(231, ex.Message);
            var changeEvent = new ModulePackageOperationsChanged([], error)
            {
                CorrelationId = correlationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Dispatching {OperationsCount} package operation(s) correlated by {CorrelationId} to {InstanceCount} instance(s)")]
    private static partial void LogDispatch(ILogger<UpdateModulePackageOperationsConsumer> logger, Guid correlationId, int operationsCount, int instanceCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to dispatch {OperationCount} operations to the cluster.")]
    private static partial void LogError(ILogger<UpdateModulePackageOperationsConsumer> logger, Exception exception, int operationCount);
}
