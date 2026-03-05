using Core.Shared.Modules.Commands;
using Core.Shared.Modules.Events;
using MassTransit;
using Sdk.Messaging;

namespace Core.OS.Modules.Consumers;

public sealed class UpdateModulePackageOperationsConsumer(IModulePackageOperationStore store) : IConsumer<UpdateModulePackageOperations>
{
    public async Task Consume(ConsumeContext<UpdateModulePackageOperations> context)
    {
        try
        {
            var changes = await store.EnqueueOperations(context.Message.Operations, context.CancellationToken);

            await context.Publish(new ModulePackageOperationsChanged(changes));
        }
        catch (Exception ex)
        {
            var error = new ErrorInfo(230, ex.Message);

            await context.Publish(new ModulePackageOperationsFailed(context.Message.CorrelationId, error));
        }
    }
}
