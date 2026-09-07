using Core.Shared.Modules;
using Core.Shared.Modules.Commands;
using Core.Shared.Modules.Events;
using MassTransit;
using Sdk.Instance;
using Sdk.Messaging;

namespace Core.OS.Modules.Consumers;

/// <summary>
/// Publishes the correlated failure feedback for <see cref="EnqueueModulePackageOperations"/> once
/// <see cref="EnqueueModulePackageOperationsConsumer"/> has exhausted its retry ladder. See ADR-004 (D6).
/// </summary>
/// <remarks>
/// The command consumer cannot report its own failure: it has to rethrow to be retried, and the in-memory outbox
/// discards everything a faulted consumer published. MassTransit publishes <see cref="Fault{T}"/> from the error pipe,
/// outside that outbox scope, which is why the feedback lives here.
/// <para>
/// Deliberately not a <c>[ReadOnlyConsumer]</c>: the fault is a plain, instance-independent event, so only the master
/// (or a standalone) runs this consumer.
/// </para>
/// <para>
/// That is not by itself enough to report once. <c>UpdateModulePackageOperationsConsumer</c> fans the command out as
/// one copy per instance, all sharing a single <c>CorrelationId</c>, so every failing node raises its own
/// <see cref="Fault{T}"/> and this one consumer sees all of them. The UI-facing
/// <see cref="ModulePackageOperationsChanged"/> is therefore published only for the local node's own copy — the same
/// thing the consumer's catch block did before, where the <c>Changed</c> event was gated on the publisher not being a
/// slave. The per-node <see cref="ModulePackageOperationsEnqueued"/> stays unconditional: it carries the instance id
/// and is not forwarded to the UI, so it is the right place for every node's outcome.
/// </para>
/// </remarks>
public sealed partial class EnqueueModulePackageOperationsFaultConsumer(
    IInstanceInformationProvider instanceInfoProvider,
    ILogger<EnqueueModulePackageOperationsFaultConsumer> logger) : IConsumer<Fault<EnqueueModulePackageOperations>>
{
    public async Task Consume(ConsumeContext<Fault<EnqueueModulePackageOperations>> context)
    {
        var command = context.Message.Message;
        var reason = context.Message.Exceptions.FirstOrDefault()?.Message ?? "Unknown error";

        LogFault(logger, command.CorrelationId, command.InstanceId, reason);

        var error = new ErrorInfo(ModuleErrorCodes.EnqueueOperationsFailed, reason);

        // Per-node fact for backend correlation.
        await context.Publish(new ModulePackageOperationsEnqueued(command.InstanceId, [], error) { CorrelationId = command.CorrelationId },
            context.CancellationToken);

        // One correlated UI report per operation, not one per failing node. Publishing this for a remote node's fault
        // would put a second, contradictory verdict on a correlation id the local node may already have completed -
        // and because the client completes on whichever verdict arrives first, the two would race.
        if (command.InstanceId != instanceInfoProvider.Local.Id)
            return;

        await context.Publish(new ModulePackageOperationsChanged([], error) { CorrelationId = command.CorrelationId },
            context.CancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Enqueueing package operations on instance {InstanceId} failed permanently, correlated by {CorrelationId}: {Reason}")]
    private static partial void LogFault(ILogger<EnqueueModulePackageOperationsFaultConsumer> logger, Guid correlationId, Guid instanceId,
        string reason);
}
