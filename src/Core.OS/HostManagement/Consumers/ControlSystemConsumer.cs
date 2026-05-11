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
using Sdk.SystemConfiguration.Events;

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
        var correlationId = context.Message.CorrelationId;
        var command = context.Message.Command;

        LogConsume(logger, correlationId, command);

        try
        {
            var result = command switch
            {
                SystemCommand.Reset => await ProcessResetSystem(context),
                SystemCommand.Restart => await ProcessRestartSystem(context),
                SystemCommand.Shutdown => await ProcessShutdownSystem(context),
                _ => throw new UnreachableException(),
            };

            if (result?.Status != OperationStatus.Success)
            {
                var errorResponse = new ControlSystemCompleted(command)
                {
                    CorrelationId = correlationId,
                    ErrorInfo = new ErrorInfo((int?)result?.Status ?? ControlSystemCompleted.UnknownError, result?.Message)
                };

                await context.Publish(errorResponse, context.CancellationToken);
                return;
            }

            var response = new ControlSystemCompleted(command) { CorrelationId = correlationId };

            await context.Publish(response, context.CancellationToken);

            if (command == SystemCommand.Reset)
            {
                var restartResult = await pipeClient.RestartSuite(instanceOptions.Value, context.CancellationToken);
                if (restartResult.Status == OperationStatus.Error)
                    throw new InvalidOperationException($"Restart service='{instanceOptions.Value.ServiceName}' failed with {restartResult.Message}");
            }
        }
        catch (Exception e)
        {
            LogUnexpectedError(logger, e, correlationId, command);

            var response = new ControlSystemCompleted(command)
            {
                CorrelationId = correlationId,
                ErrorInfo = new ErrorInfo(ControlServiceErrorCodes.UnknownError, e.Message)
            };

            await context.Publish(response, context.CancellationToken);
        }
    }

    private async Task<OperationResult?> ProcessResetSystem(ConsumeContext<ControlSystem> context)
    {
        // what to do now set reset for suite
        var result = await pipeClient.ResetSystem(context.CancellationToken);
        if (result?.Status != OperationStatus.Success)
            return result;

        // HM reset sent successfully so trigger reset on restart for suite
        LogSuiteResetFlag(logger, context.Message.CorrelationId);

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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Executing command='{Command}' on system correlated by {CorrelationId}")]
    private static partial void LogConsume(ILogger logger, Guid correlationId, SystemCommand command);

    [LoggerMessage(Level = LogLevel.Information, Message = "Set suite reset flag because host management operation succeeded correlated by {CorrelationId}")]
    private static partial void LogSuiteResetFlag(ILogger logger, Guid correlationId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error occured while executing command='{Command}' on system correlated by {CorrelationId}")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception, Guid correlationId, SystemCommand command);
}
