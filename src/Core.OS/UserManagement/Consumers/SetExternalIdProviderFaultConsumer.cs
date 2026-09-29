using Core.Shared.UserManagement.Commands;
using Core.Shared.UserManagement.Events;
using MassTransit;
using Sdk.Messaging;

namespace Core.OS.UserManagement.Consumers;

/// <summary>
/// Reports the correlated failure for <see cref="SetExternalIdProvider"/> once
/// <see cref="SetExternalIdProviderConsumer"/> has exhausted its retry ladder. See ADR-004 (D6).
/// </summary>
/// <remarks>
/// Deliberately not a <c>[ReadOnlyConsumer]</c>: only the master (or a standalone) stores the provider and
/// raises the fault.
/// </remarks>
public sealed partial class SetExternalIdProviderFaultConsumer(
    ILogger<SetExternalIdProviderFaultConsumer> logger) : IConsumer<Fault<SetExternalIdProvider>>
{
    public async Task Consume(ConsumeContext<Fault<SetExternalIdProvider>> context)
    {
        var correlationId = context.Message.Message.CorrelationId;
        var reason = context.Message.Exceptions.FirstOrDefault()?.Message ?? "Unknown error";

        LogFault(logger, correlationId, reason);

        await context.Publish(
            new SetExternalIdProviderError(correlationId, new ErrorInfo(UserErrorCodes.UnknownError, reason)),
            context.CancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Storing the OpenID provider failed permanently, correlated by {CorrelationId}: {Reason}")]
    private static partial void LogFault(ILogger<SetExternalIdProviderFaultConsumer> logger, Guid correlationId, string reason);
}
