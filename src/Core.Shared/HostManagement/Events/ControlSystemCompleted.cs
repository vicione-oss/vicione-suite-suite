using Core.Shared.HostManagement.Commands;
using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.HostManagement.Events;

[ForwardToUI]
public record ControlSystemCompleted(Guid CorrelationId, SystemCommand Command) : IEvent, CorrelatedBy<Guid>;
