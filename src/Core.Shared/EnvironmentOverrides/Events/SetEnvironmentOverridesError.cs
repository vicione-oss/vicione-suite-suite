using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.EnvironmentOverrides.Events;

[ForwardToUI]
public record SetEnvironmentOverridesError(Guid CorrelationId, ErrorInfo Error) : IEvent, CorrelatedBy<Guid>;
