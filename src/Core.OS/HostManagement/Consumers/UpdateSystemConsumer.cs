using System.Text.Json;
using Core.OS.UserManagement.Extensions;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Events;
using Core.Shared.UserManagement.Contracts;
using HostManagement.Shared.Communication;
using HostManagement.Shared.Communication.Contracts;
using HostManagement.Shared.Communication.Enums;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Sdk.Messaging;
using Sdk.SystemConfiguration;

namespace Core.OS.HostManagement.Consumers;

[ReadOnlyConsumer]
public sealed class UpdateSystemConsumer(IPipeClient pipeClient, UserManager<SuiteUser> userManager, ILogger<UpdateSystemConsumer> logger) : IConsumer<UpdateSystem>
{
    public async Task Consume(ConsumeContext<UpdateSystem> context)
    {
        var correlationId = context.CorrelationId ?? context.Message.CorrelationId;

        logger.LogInformation("Consuming {Command} with CorrelationId '{Id}'", nameof(UpdateSystem), correlationId);

        try
        {
            await userManager.InvalidateLogins();

            var resultJson = await pipeClient.SendRequest(Topics.UpdateSystem,
                context.Message.FilePath,
                context.CancellationToken);

            var result = JsonSerializer.Deserialize(resultJson, SourceGenerationContext.Default.UpdateSystemResult);
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
            logger.LogError(e, "Error occured while initiating the update from '{FilePath}'", context.Message.FilePath);
        }
    }
}
