using Core.Shared.Modules.Commands;
using Core.Shared.Modules.Events;
using MassTransit;
using Sdk.Messaging;

namespace Core.OS.Modules.Consumers;

public sealed partial class UpdateModulePackageOperationsConsumer(IModulePackageOperationStore store, ILogger<UpdateModulePackageOperationsConsumer> logger) : IConsumer<UpdateModulePackageOperations>
{
    public async Task Consume(ConsumeContext<UpdateModulePackageOperations> context)
    {
        var correlationId = context.Message.CorrelationId;

        LogConsume(logger, correlationId, context.Message.Operations.Count);

        try
        {
            var changes = await store.EnqueueOperations(context.Message.Operations, context.CancellationToken);

            var changeEvent = new ModulePackageOperationsChanged(changes)
            {
                CorrelationId = correlationId
            };

            LogOperationsUpdated(logger, correlationId, changes.Count);

            await context.Publish(changeEvent, context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogError(logger, context.Message.Operations.Count);

            var error = new ErrorInfo(230, ex.Message);
            var changeEvent = new ModulePackageOperationsChanged([], error)
            {
                CorrelationId = correlationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consume update of {OperationsCount} package operation correlated by {CorrelationId}")]
    private static partial void LogConsume(ILogger<UpdateModulePackageOperationsConsumer> logger, Guid correlationId, int operationsCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updated {OperationsCount} package operations correlated by {CorrelationId}")]
    private static partial void LogOperationsUpdated(ILogger<UpdateModulePackageOperationsConsumer> logger, Guid correlationId, int operationsCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to enqueue {OperationCount} operations.")]
    private static partial void LogError(ILogger<UpdateModulePackageOperationsConsumer> logger, int operationCount);
}
