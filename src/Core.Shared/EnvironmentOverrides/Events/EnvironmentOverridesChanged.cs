using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.EnvironmentOverrides.Events;

[ForwardToUI]
public record EnvironmentOverridesChanged(Guid CorrelationId) : IEvent, CorrelatedBy<Guid>;
