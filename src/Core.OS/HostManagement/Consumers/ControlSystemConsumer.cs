using System.Diagnostics;
using System.IO.Abstractions;
using Core.OS.HostManagement.Extensions;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.MessageBus.Extensions;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Events;
using Core.Shared.Instance.Contracts;
using HostManagement.Shared.Communication.Contracts;
using HostManagement.Shared.Communication.Enums;
using MassTransit;
using Microsoft.Extensions.Options;
using Sdk.Messaging;
using Sdk.SystemConfiguration;

namespace Core.OS.HostManagement.Consumers;

[ReadOnlyConsumer]
public sealed class ControlSystemConsumer(
    IPipeClient pipeClient,
    IFileSystem fileSystem,
    IOptions<InstanceOptions> instanceOptions,
    ILocalInstanceInformationProvider informationProvider,
    ILogger<ControlSystemConsumer> logger) : IConsumer<ControlSystem>
{
    public async Task Consume(ConsumeContext<ControlSystem> context)
    {
        var correlationId = context.CorrelationId ?? Guid.Empty;

        try
        {
            if (correlationId == Guid.Empty)
                throw new InvalidOperationException("Command can't be correlated.");

            OperationResult? result = null;

            switch (context.Message.Command)
            {
                case SystemCommand.Reset:
                    result = await ProcessResetSystem(context);
                    break;

                case SystemCommand.Restart:
                    result = await ProcessRestartSystem(context);
                    break;

                default:
                    throw new UnreachableException();
            }

            if (result?.Status != OperationStatus.Success)
            {
                await context.Publish(new ControlSystemError(correlationId,
                        context.Message.Command,
                        new ErrorInfo(
                            (int?)result?.Status ?? ControlSystemError.UnknownError,
                            result?.Message)),
                    context.CancellationToken);
                return;
            }

            await context.Publish(new ControlSystemCompleted(correlationId, context.Message.Command), context.CancellationToken);

            if (context.Message.Command == SystemCommand.Reset)
            {
                var shutdown = new ShutdownInstance
                {
                    InstanceId = informationProvider.Local.Id,
                    Reason = "Reset was requested by user",
                    Delay = TimeSpan.FromSeconds(2),
                };

                // It's an IInstanceDependentCommand but per context we can just publish it
                await context.SendToInstance(shutdown, informationProvider.Local.Id, context.CancellationToken);
            }
        }
        catch (Exception e)
        {
            await context.Publish(new ControlSystemError(correlationId, context.Message.Command, new ErrorInfo(ControlServiceError.UnknownError, e.Message)), context.CancellationToken);
            logger.LogError(e, "Error occured while executing control system '{ServiceName}'", context.Message.Command);
        }
    }

    private async Task<OperationResult?> ProcessResetSystem(ConsumeContext<ControlSystem> context)
    {
        // what to do now set reset for suite
        var result = await pipeClient.SendResetSystem(context.CancellationToken);
        if (result?.Status != OperationStatus.Success)
            return result;

        // HM reset sent successfully so trigger reset on restart for suite
        logger.LogInformation("Set suite reset flag because host management operation succeeded.");
        fileSystem.WriteResetFile(instanceOptions.Value);

        return result;
    }

    private async Task<OperationResult?> ProcessRestartSystem(ConsumeContext<ControlSystem> context)
        => await pipeClient.SendRestartSystem(context.CancellationToken);
}
