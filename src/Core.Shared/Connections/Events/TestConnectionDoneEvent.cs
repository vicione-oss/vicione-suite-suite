using Core.Shared.Connections.Contracts;
using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.Connections.Events;

[ForwardToUI]
public sealed record TestConnectionDoneEvent(Guid CorrelationId, TestConnectionResult TestResult) : IEvent, CorrelatedBy<Guid>;
