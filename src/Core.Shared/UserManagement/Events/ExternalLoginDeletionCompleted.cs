using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.UserManagement.Events;

[ForwardToUI]
public record ExternalLoginDeletionCompleted(Guid CorrelationId) : IEvent, CorrelatedBy<Guid>
{
    public ErrorInfo? ErrorInfo { get; init; }
}