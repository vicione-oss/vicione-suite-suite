using Core.Shared.Modules;
using Core.Shared.Modules.Commands;
using Core.Shared.Modules.Events;
using MassTransit;
using Sdk.Instance;
using Sdk.Messaging;

namespace Core.OS.Modules.Consumers;

[ReadOnlyConsumer]
public sealed partial class EnqueueModulePackageOperationsConsumer(
    IModulePackageOperationStore store,
    IInstanceInformationProvider instanceInfoProvider,
    ILogger<EnqueueModulePackageOperationsConsumer> logger) : IConsumer<EnqueueModulePackageOperations>
{
    public async Task Consume(ConsumeContext<EnqueueModulePackageOperations> context)
    {
        var correlationId = context.Message.CorrelationId;
        var instanceId = context.Message.InstanceId;

        LogConsume(logger, correlationId, context.Message.Operations.Count);

        try
        {
            var changes = await store.EnqueueOperations(context.Message.Operations, context.CancellationToken);

            LogOperationsUpdated(logger, correlationId, changes.Count);

            // Per-node fact for backend correlation (foundation for a future update/restart saga).
            await context.Publish(new ModulePackageOperationsEnqueued(instanceId, changes) { CorrelationId = correlationId }, context.CancellationToken);

            // Only the master (or a standalone) forwards the change to the UI to keep a single, correlated UI update.
            if (instanceInfoProvider.Local.Type != InstanceType.Slave)
                await context.Publish(new ModulePackageOperationsChanged(changes) { CorrelationId = correlationId }, context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogError(logger, ex, context.Message.Operations.Count);

            var error = new ErrorInfo(ModuleErrorCodes.EnqueueOperationsFailed, ex.Message);

            await context.Publish(new ModulePackageOperationsEnqueued(instanceId, [], error) { CorrelationId = correlationId }, context.CancellationToken);

            if (instanceInfoProvider.Local.Type != InstanceType.Slave)
                await context.Publish(new ModulePackageOperationsChanged([], error) { CorrelationId = correlationId }, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consume enqueue of {OperationsCount} package operation correlated by {CorrelationId}")]
    private static partial void LogConsume(ILogger<EnqueueModulePackageOperationsConsumer> logger, Guid correlationId, int operationsCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Enqueued {OperationsCount} package operations correlated by {CorrelationId}")]
    private static partial void LogOperationsUpdated(ILogger<EnqueueModulePackageOperationsConsumer> logger, Guid correlationId, int operationsCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to enqueue {OperationCount} operations.")]
    private static partial void LogError(ILogger<EnqueueModulePackageOperationsConsumer> logger, Exception error, int operationCount);
}
