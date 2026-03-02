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
        var correlationId = context.CorrelationId ?? context.Message.CorrelationId;

        LogConsumingCommand(logger, nameof(UpdateSystem), correlationId);

        try
        {
            await userManager.InvalidateLogins();

            var result = await pipeClient.UpdateSystem(context.Message.FilePath, context.CancellationToken);
            if (result is not null && result.Status != OperationStatus.Error)
            {
                await context.Publish(new UpdateSystemStarted(correlationId, result.Message, result.Status == OperationStatus.Warning),
                    context.CancellationToken);
            }
            else
            {
                await context.Publish(new UpdateSystemError(correlationId, new ErrorInfo((int?)result?.Status ?? UpdateSystemError.UnknownError, result?.Message)),
                    context.CancellationToken);
            }
        }
        catch (Exception e)
        {
            await context.Publish(new UpdateSystemError(correlationId, new ErrorInfo(ControlServiceError.UnknownError, e.Message)), context.CancellationToken);
            LogUpdateSystemError(logger, e, context.Message.FilePath);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Consuming {Command} with CorrelationId '{Id}'")]
    private static partial void LogConsumingCommand(ILogger logger, string Command, object? Id);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error occured while initiating the update from '{FilePath}'")]
    private static partial void LogUpdateSystemError(ILogger logger, Exception exception, string FilePath);
}
