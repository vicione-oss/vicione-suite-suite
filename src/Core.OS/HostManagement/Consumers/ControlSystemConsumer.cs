using System.Diagnostics;
using System.IO.Abstractions;
using Core.OS.HostManagement.Extensions;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.UserManagement.Extensions;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Events;
using Core.Shared.UserManagement.Contracts;
using HostManagement.Shared.Communication.Contracts;
using HostManagement.Shared.Communication.Enums;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Sdk.Messaging;
using Sdk.SystemConfiguration;

namespace Core.OS.HostManagement.Consumers;

[ReadOnlyConsumer]
public sealed partial class ControlSystemConsumer(
    IPipeClient pipeClient,
    IFileSystem fileSystem,
    UserManager<SuiteUser> userManager,
    IOptions<InstanceOptions> instanceOptions,
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

                case SystemCommand.Shutdown:
                    result = await ProcessShutdownSystem(context);
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
                var restartResult = await pipeClient.RestartSuite(instanceOptions.Value, context.CancellationToken);
                if (restartResult.Status == OperationStatus.Error)
                    throw new InvalidOperationException($"Restart service='{instanceOptions.Value.ServiceName}' failed with {restartResult.Message}");
            }
        }
        catch (Exception e)
        {
            LogControlSystemError(logger, e, context.Message.Command);

            await context.Publish(new ControlSystemError(correlationId, context.Message.Command, new ErrorInfo(ControlServiceError.UnknownError, e.Message)), context.CancellationToken);
        }
    }

    private async Task<OperationResult?> ProcessResetSystem(ConsumeContext<ControlSystem> context)
    {
        // what to do now set reset for suite
        var result = await pipeClient.ResetSystem(context.CancellationToken);
        if (result?.Status != OperationStatus.Success)
            return result;

        // HM reset sent successfully so trigger reset on restart for suite
        LogSuiteResetFlag(logger);

        fileSystem.WriteResetFile(instanceOptions.Value);

        return result;
    }

    private async Task<OperationResult?> ProcessShutdownSystem(ConsumeContext<ControlSystem> context)
    {
        var result = await pipeClient.ShutdownSystem(context.CancellationToken);
        if (result is not null && result.Status != OperationStatus.Error)
        {
            await userManager.InvalidateLogins();
        }

        return result;
    }

    private async Task<OperationResult?> ProcessRestartSystem(ConsumeContext<ControlSystem> context)
        => await pipeClient.RestartSystem(context.CancellationToken);

    [LoggerMessage(Level = LogLevel.Information, Message = "Set suite reset flag because host management operation succeeded.")]
    private static partial void LogSuiteResetFlag(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error occured while executing control system '{ServiceName}'")]
    private static partial void LogControlSystemError(ILogger logger, Exception exception, SystemCommand ServiceName);
}
