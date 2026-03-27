using MassTransit;
using Sdk.Messaging;
using Sdk.SystemConfiguration;
using Sdk.SystemConfiguration.Commands;
using Sdk.SystemConfiguration.Contracts;
using Sdk.SystemConfiguration.Events;

namespace Core.OS.HostManagement.Consumers;

[ReadOnlyConsumer]
public sealed partial class ControlServiceConsumer(IControlServiceManagement serviceManagement, ILogger<ControlServiceConsumer> logger) : IConsumer<ControlService>
{
    public async Task Consume(ConsumeContext<ControlService> context)
    {
        var correlationId = context.Message.CorrelationId;

        try
        {
            if (correlationId == Guid.Empty)
                throw new InvalidOperationException("Command can't be correlated.");

            LogControlService(logger, context.Message.ServiceName, context.Message.Command);

            var result = await serviceManagement.TryControlService(context.Message.Command, context.Message.ServiceName, context.CancellationToken);

            if (!result.Success)
            {
                var errorResponse = new ControlServiceCompleted(context.Message.ServiceName, result.State)
                {
                    CorrelationId = correlationId,
                    ErrorInfo = result.Error!
                };

                await context.Publish(errorResponse, context.CancellationToken);
                return;
            }

            await context.Publish(new SystemConfigurationChanged { CorrelationId = correlationId }, context.CancellationToken);

            var response = new ControlServiceCompleted(context.Message.ServiceName, result.State) { CorrelationId = correlationId };
            await context.Publish(response, context.CancellationToken);
        }
        catch (Exception e)
        {
            LogControlServiceError(logger, e, context.Message.ServiceName);

            var errorResponse = new ControlServiceCompleted(context.Message.ServiceName, ServiceState.Unknown)
            {
                CorrelationId = correlationId,
                ErrorInfo = new ErrorInfo(ControlServiceErrorCodes.UnknownError, e.Message)
            };

            await context.Publish(errorResponse, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Executing {Command} on service '{ServiceName}'")]
    private static partial void LogControlService(ILogger logger, string serviceName, ServiceCommand command);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error occured while executing control service '{ServiceName}'")]
    private static partial void LogControlServiceError(ILogger logger, Exception exception, string serviceName);
}
