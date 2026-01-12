using Core.Shared.Instance.Contracts;
using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.Instance.Events;

[ForwardToUI]
public sealed record CrossInstanceConfigurationChanged(Guid CorrelationId, CrossInstanceConfiguration CrossInstanceConfiguration) : IEvent, CorrelatedBy<Guid>;
