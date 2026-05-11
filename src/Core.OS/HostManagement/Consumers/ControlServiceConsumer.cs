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
        var serviceName = context.Message.ServiceName;
        var command = context.Message.Command;

        LogConsume(logger, correlationId, serviceName, command);

        try
        {
            var result = await serviceManagement.TryControlService(command, serviceName, context.CancellationToken);
            if (!result.Success)
            {
                LogControlNoSuccess(logger, correlationId, serviceName, command);

                var errorResponse = new ControlServiceCompleted(serviceName, result.State)
                {
                    CorrelationId = correlationId,
                    ErrorInfo = result.Error!
                };

                await context.Publish(errorResponse, context.CancellationToken);
                return;
            }

            LogControlSuccess(logger, correlationId, serviceName, command);

            await context.Publish(new SystemConfigurationChanged { CorrelationId = correlationId }, context.CancellationToken);

            var response = new ControlServiceCompleted(serviceName, result.State) { CorrelationId = correlationId };
            await context.Publish(response, context.CancellationToken);
        }
        catch (Exception e)
        {
            LogUnexpectedError(logger, e, correlationId, serviceName, command);

            var errorResponse = new ControlServiceCompleted(serviceName, ServiceState.Unknown)
            {
                CorrelationId = correlationId,
                ErrorInfo = new ErrorInfo(ControlServiceErrorCodes.UnknownError, e.Message)
            };

            await context.Publish(errorResponse, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Executing {Command} on service '{ServiceName}' correlated by {CorrelationId}")]
    private static partial void LogConsume(ILogger logger, Guid correlationId, string serviceName, ServiceCommand command);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Executed command='{Command}' on service='{ServiceName}' correlated by {CorrelationId} successfully")]
    private static partial void LogControlSuccess(ILogger logger, Guid correlationId, string serviceName, ServiceCommand command);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Execution command='{Command}' on service '{ServiceName}' correlated by {CorrelationId} was not successfully")]
    private static partial void LogControlNoSuccess(ILogger logger, Guid correlationId, string serviceName, ServiceCommand command);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error occurred while executing command='{Command}' on service '{ServiceName}' correlated by {CorrelationId}")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception, Guid correlationId, string serviceName, ServiceCommand command);
}
