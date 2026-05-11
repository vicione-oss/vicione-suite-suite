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
        var filePath = context.Message.FilePath;

        LogConsume(logger, correlationId, filePath);

        try
        {
            await userManager.InvalidateLogins();

            var result = await pipeClient.UpdateSystem(filePath, context.CancellationToken);
            if (result is null || result.Status == OperationStatus.Error)
            {
                LogUpdateFailed(logger, correlationId, filePath);

                var errorResponse = new UpdateSystemStarted(null, false)
                {
                    CorrelationId = correlationId,
                    ErrorInfo = new ErrorInfo((int?)result?.Status ?? UpdateSystemStarted.UnknownError, result?.Message)
                };

                await context.Publish(errorResponse, context.CancellationToken);
                return;
            }

            var startedEvent = new UpdateSystemStarted(result.Message, result.Status == OperationStatus.Warning)
            {
                CorrelationId = correlationId
            };

            await context.Publish(startedEvent, context.CancellationToken);
        }
        catch (Exception e)
        {
            LogUnexpectedError(logger, e, correlationId, filePath);

            var errorResponse = new UpdateSystemStarted(null, false)
            {
                CorrelationId = correlationId,
                ErrorInfo = new ErrorInfo(ControlServiceErrorCodes.UnknownError, e.Message)
            };

            await context.Publish(errorResponse, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Updating system from file='{FilePath}' correlated by {CorrelationId}")]
    private static partial void LogConsume(ILogger logger, Guid correlationId, string filePath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to update system from file='{FilePath}' correlated by {CorrelationId}")]
    private static partial void LogUpdateFailed(ILogger logger, Guid correlationId, string filePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error occured while initiating the update from '{FilePath}' correlated by {CorrelationId}")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception, Guid correlationId, string filePath);
}
