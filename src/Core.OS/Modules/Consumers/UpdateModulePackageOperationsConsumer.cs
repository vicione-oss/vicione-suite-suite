using Core.Shared.Modules.Commands;
using Core.Shared.Modules.Events;
using MassTransit;
using Sdk.Messaging;

namespace Core.OS.Modules.Consumers;

public sealed partial class UpdateModulePackageOperationsConsumer(IModulePackageOperationStore store, ILogger<UpdateModulePackageOperationsConsumer> logger) : IConsumer<UpdateModulePackageOperations>
{
    public async Task Consume(ConsumeContext<UpdateModulePackageOperations> context)
    {
        try
        {
            var changes = await store.EnqueueOperations(context.Message.Operations, context.CancellationToken);

            var changeEvent = new ModulePackageOperationsChanged(changes)
            {
                CorrelationId = context.Message.CorrelationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogError(logger, context.Message.Operations.Count);

            var error = new ErrorInfo(230, ex.Message);
            var changeEvent = new ModulePackageOperationsChanged([], error)
            {
                CorrelationId = context.Message.CorrelationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to enqueue {OperationCount} operations.")]
    private static partial void LogError(ILogger<UpdateModulePackageOperationsConsumer> logger, int operationCount);
}
