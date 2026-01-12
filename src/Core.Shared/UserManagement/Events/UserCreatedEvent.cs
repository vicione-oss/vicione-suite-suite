using Core.Shared.UserManagement.Contracts;
using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.UserManagement.Events;

[ForwardToUI]
public sealed record UserCreatedEvent(Guid CorrelationId, UserProfile UserProfile) : IEvent, CorrelatedBy<Guid>;
