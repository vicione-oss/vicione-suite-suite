using Core.Shared.UserManagement.Contracts;
using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.UserManagement.Events;

[ForwardToUI]
public sealed record UserUpdatedEvent(Guid CorrelationId, UserProfile UserProfile, UserProfile UserProfileBefore)
    : IEvent, CorrelatedBy<Guid>;
