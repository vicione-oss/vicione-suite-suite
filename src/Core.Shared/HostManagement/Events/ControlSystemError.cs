using Core.Shared.HostManagement.Commands;
using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.HostManagement.Events;

[ForwardToUI]
public record ControlSystemError(Guid CorrelationId, SystemCommand Command, ErrorInfo Error) : IEvent, CorrelatedBy<Guid>
{
    public const int UnknownError = -1;
}
