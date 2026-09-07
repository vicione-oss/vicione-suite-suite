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
            // ADR-004 (D6): log and rethrow so the retry ladder applies and the command finally dead-letters. The
            // correlated failure feedback is published by EnqueueModulePackageOperationsFaultConsumer - publishing it
            // from here is impossible, because the in-memory outbox discards everything a faulted consumer published.
            LogError(logger, ex, correlationId, instanceId, context.Message.Operations.Count);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consume enqueue of {OperationsCount} package operation correlated by {CorrelationId}")]
    private static partial void LogConsume(ILogger<EnqueueModulePackageOperationsConsumer> logger, Guid correlationId, int operationsCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Enqueued {OperationsCount} package operations correlated by {CorrelationId}")]
    private static partial void LogOperationsUpdated(ILogger<EnqueueModulePackageOperationsConsumer> logger, Guid correlationId, int operationsCount);

    // Where the transport discards faulted messages - the in-memory bus on Standalone - this line is the only
    // post-mortem evidence of the failure, so it carries the ids needed to tie it back to the operation the user
    // triggered. See ADR-004 (D5, D6).
    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to enqueue {OperationCount} package operations on instance {InstanceId} correlated by {CorrelationId}")]
    private static partial void LogError(ILogger<EnqueueModulePackageOperationsConsumer> logger, Exception error, Guid correlationId,
        Guid instanceId, int operationCount);
}
