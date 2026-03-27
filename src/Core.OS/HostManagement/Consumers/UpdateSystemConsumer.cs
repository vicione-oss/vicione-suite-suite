using Core.OS.HostManagement.Extensions;
using Core.OS.UserManagement.Extensions;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Events;
using Core.Shared.UserManagement.Contracts;
using HostManagement.Shared.Communication.Enums;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Sdk.Messaging;
using Sdk.SystemConfiguration.Events;

namespace Core.OS.HostManagement.Consumers;

[ReadOnlyConsumer]
public sealed partial class UpdateSystemConsumer(IPipeClient pipeClient, UserManager<SuiteUser> userManager, ILogger<UpdateSystemConsumer> logger) : IConsumer<UpdateSystem>
{
    public async Task Consume(ConsumeContext<UpdateSystem> context)
    {
        var correlationId = context.Message.CorrelationId;

        try
        {
            await userManager.InvalidateLogins();

            var result = await pipeClient.UpdateSystem(context.Message.FilePath, context.CancellationToken);
            if (result is not null && result.Status != OperationStatus.Error)
            {
                var startedEvent = new UpdateSystemStarted(result.Message, result.Status == OperationStatus.Warning)
                {
                    CorrelationId = correlationId
                };

                await context.Publish(startedEvent, context.CancellationToken);
            }
            else
            {
                var errorResponse = new UpdateSystemStarted(null, false)
                {
                    CorrelationId = correlationId,
                    ErrorInfo = new ErrorInfo((int?)result?.Status ?? UpdateSystemStarted.UnknownError, result?.Message)
                };

                await context.Publish(errorResponse, context.CancellationToken);
            }
        }
        catch (Exception e)
        {
            LogUpdateSystemError(logger, e, context.Message.FilePath);

            var errorResponse = new UpdateSystemStarted(null, false)
            {
                CorrelationId = correlationId,
                ErrorInfo = new ErrorInfo(ControlServiceErrorCodes.UnknownError, e.Message)
            };

            await context.Publish(errorResponse, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Error occured while initiating the update from '{FilePath}'")]
    private static partial void LogUpdateSystemError(ILogger logger, Exception exception, string FilePath);
}
