using Core.Shared.Connections.Contracts;
using Sdk.Messaging;

namespace Core.Shared.Connections.Events;

[ForwardToUI]
public sealed record TestConnectionDoneEvent(Guid RequestId, TestConnectionResult TestResult) : IEvent;
