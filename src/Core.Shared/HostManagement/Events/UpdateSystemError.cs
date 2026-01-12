using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.HostManagement.Events;

[ForwardToUI]
public record UpdateSystemError(Guid CorrelationId, ErrorInfo Error) : IEvent, CorrelatedBy<Guid>
{
    public const int UnknownError = -1;
}
