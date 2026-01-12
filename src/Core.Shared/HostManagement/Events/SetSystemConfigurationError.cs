using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.HostManagement.Events;

[ForwardToUI]
public record SetSystemConfigurationError(Guid CorrelationId, ErrorInfo Error) : IEvent, CorrelatedBy<Guid>;
