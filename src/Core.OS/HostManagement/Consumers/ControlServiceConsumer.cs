using MassTransit;
using Sdk.Messaging;
using Sdk.SystemConfiguration;
using Sdk.SystemConfiguration.Events;

namespace Core.OS.HostManagement.Consumers;

[ReadOnlyConsumer]
public sealed class ControlServiceConsumer(IControlServiceManagement serviceManagement, ILogger<ControlServiceConsumer> logger) : IConsumer<ControlService>
{
    public async Task Consume(ConsumeContext<ControlService> context)
    {
        var correlationId = context.CorrelationId ?? Guid.Empty;

        try
        {
            if (correlationId == Guid.Empty)
                throw new InvalidOperationException("Command can't be correlated.");

            var result = await serviceManagement.ControlService(context.Message.Command, context.Message.ServiceName, context.CancellationToken);

            if (!result.Success)
            {
                await context.Publish(new ControlServiceError(correlationId, context.Message.ServiceName, result.Error!), context.CancellationToken);
                return;
            }

            await context.Publish(new SystemConfigurationChanged(correlationId), context.CancellationToken);
            await context.Publish(new ControlServiceCompleted(correlationId, context.Message.ServiceName, result.State), context.CancellationToken);
        }
        catch (Exception e)
        {
            await context.Publish(new ControlServiceError(correlationId, context.Message.ServiceName, new ErrorInfo(ControlServiceError.UnknownError, e.Message)), context.CancellationToken);
            logger.LogError(e, "Error occured while executing control service '{ServiceName}'", context.Message.ServiceName);
        }
    }
}
