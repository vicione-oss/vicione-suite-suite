using MassTransit;
using Sdk.Messaging;
using Sdk.SystemConfiguration;
using Sdk.SystemConfiguration.Commands;
using Sdk.SystemConfiguration.Events;

namespace Core.OS.HostManagement.Consumers;

[ReadOnlyConsumer]
public sealed partial class ControlServiceConsumer(IControlServiceManagement serviceManagement, ILogger<ControlServiceConsumer> logger) : IConsumer<ControlService>
{
    public async Task Consume(ConsumeContext<ControlService> context)
    {
        var correlationId = context.CorrelationId ?? Guid.Empty;

        try
        {
            if (correlationId == Guid.Empty)
                throw new InvalidOperationException("Command can't be correlated.");

            LogControlService(logger, context.Message.ServiceName, context.Message.Command);

            var result = await serviceManagement.TryControlService(context.Message.Command, context.Message.ServiceName, context.CancellationToken);

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
            LogControlServiceError(logger, e, context.Message.ServiceName);

            await context.Publish(new ControlServiceError(correlationId, context.Message.ServiceName, new ErrorInfo(ControlServiceError.UnknownError, e.Message)), context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Executing {Command} on service '{ServiceName}'")]
    private static partial void LogControlService(ILogger logger, string serviceName, ServiceCommand command);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error occured while executing control service '{ServiceName}'")]
    private static partial void LogControlServiceError(ILogger logger, Exception exception, string serviceName);
}
