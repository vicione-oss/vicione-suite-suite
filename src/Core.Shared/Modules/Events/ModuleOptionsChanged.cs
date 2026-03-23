using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.Modules.Events;

[ForwardToUI]
public sealed record ModuleOptionsChanged(string ModuleId, ErrorInfo? Error = null) : IEvent, CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
