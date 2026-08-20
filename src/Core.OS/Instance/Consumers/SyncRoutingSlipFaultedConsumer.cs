using Core.OS.Instance.Commands;
using Core.OS.Instance.Mappers;
using Core.OS.Instance.Services;
using MassTransit;
using Sdk.Backend.Messaging;
using Sdk.Instance.Events;
using Sdk.Messaging;

namespace Core.OS.Instance.Consumers;

/// <summary>
///     Handles routing slip faults during full-sync on slave instances.
///     Resets <see cref="SynchronizationState"/> and triggers re-registration with configurable retry limits.
///     Exempt from <see cref="SynchronizationState"/> dependency to receive messages while sync is pending.
/// </summary>
[ReadOnlyConsumer]
public sealed partial class SyncRoutingSlipFaultedConsumer(
    SynchronizationState synchronizationState,
    SyncRetryState syncRetryState,
    ILocalInstanceInformationProvider localInstanceInformationProvider,
    ISendEndpointProvider sendEndpointProvider,
    IHostApplicationLifetime applicationLifetime,
    ILogger<SyncRoutingSlipFaultedConsumer> logger) : IConsumer<InstanceSynchronizationFailed>
{
    public Task Consume(ConsumeContext<InstanceSynchronizationFailed> context)
    {
        var localInstanceId = localInstanceInformationProvider.ReadLocalInstanceId();

        if (context.Message.InstanceId != localInstanceId)
            return Task.CompletedTask;

        LogSyncFailed(logger, localInstanceId, context.Message.Errors);

        synchronizationState.Reset();

        var canRetry = syncRetryState.RecordFailure();
        if (!canRetry)
        {
            LogRetryLimitExhausted(logger, localInstanceId, syncRetryState.MaxRetries);
            return Task.CompletedTask;
        }

        LogSchedulingRetry(logger, localInstanceId, syncRetryState.AttemptCount, syncRetryState.MaxRetries, syncRetryState.RetryDelay);

        _ = SendRegistrationAfterDelay(localInstanceId, syncRetryState.RetryDelay);

        return Task.CompletedTask;
    }

    private async Task SendRegistrationAfterDelay(Guid instanceId, TimeSpan delay)
    {
        var stopping = applicationLifetime.ApplicationStopping;

        try
        {
            await Task.Delay(delay, stopping);

            var endpoint = await sendEndpointProvider.GetSendEndpoint(
                MessagingHelper.GetCommandEndpointAddress<RegisterInstance>());

            await endpoint.Send(localInstanceInformationProvider.Local.ToRegisterInstanceCommand(
                [.. localInstanceInformationProvider.LoadedModules],
                [],
                []) with { ForceSync = true }, stopping);

            LogReregistrationSent(logger, instanceId);
        }
        catch (OperationCanceledException)
        {
            // Application shutting down — expected, no action needed
        }
        catch (Exception ex)
        {
            LogReregistrationFailed(logger, ex, instanceId);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Full-sync routing slip faulted for instance {InstanceId}. Errors: {Errors}")]
    private static partial void LogSyncFailed(ILogger<SyncRoutingSlipFaultedConsumer> logger, Guid instanceId, IReadOnlyList<string> errors);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Full-sync retry limit exhausted for instance {InstanceId} after {MaxRetries} attempts. Slave is degraded — manual intervention required.")]
    private static partial void LogRetryLimitExhausted(ILogger<SyncRoutingSlipFaultedConsumer> logger, Guid instanceId, int maxRetries);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Scheduling full-sync retry {Attempt}/{MaxRetries} for instance {InstanceId} after {Delay}")]
    private static partial void LogSchedulingRetry(ILogger<SyncRoutingSlipFaultedConsumer> logger, Guid instanceId, int attempt, int maxRetries, TimeSpan delay);

    [LoggerMessage(Level = LogLevel.Information, Message = "Re-registration command sent for instance {InstanceId}")]
    private static partial void LogReregistrationSent(ILogger<SyncRoutingSlipFaultedConsumer> logger, Guid instanceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send re-registration command for instance {InstanceId}")]
    private static partial void LogReregistrationFailed(ILogger<SyncRoutingSlipFaultedConsumer> logger, Exception exception, Guid instanceId);
}
