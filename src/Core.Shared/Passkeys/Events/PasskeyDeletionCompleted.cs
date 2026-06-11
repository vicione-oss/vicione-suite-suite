using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.Passkeys.Events;

[ForwardToUI]
public record PasskeyDeletionCompleted(Guid CorrelationId) : IEvent, CorrelatedBy<Guid>
{
    public ErrorInfo? ErrorInfo { get; init; }
}
