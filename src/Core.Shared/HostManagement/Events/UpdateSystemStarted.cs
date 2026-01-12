using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.HostManagement.Events;

[ForwardToUI]
public record UpdateSystemStarted(Guid CorrelationId, string? Message, bool WithWarnings) : IEvent, CorrelatedBy<Guid>
{
}
