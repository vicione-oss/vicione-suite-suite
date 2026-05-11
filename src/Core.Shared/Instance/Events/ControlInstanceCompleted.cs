using Core.Shared.Instance.Commands;
using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.Instance.Events;

[ForwardToUI]
public sealed record ControlInstanceCompleted(Guid InstanceId, InstanceCommand Command, ErrorInfo? Error = null) : IEvent, CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
