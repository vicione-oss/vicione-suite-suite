using Core.Shared.UserManagement.Contracts;
using Sdk.Messaging;

namespace Core.Shared.UserManagement.Events;

[ForwardToUI]
public sealed record UserUpdatedEvent(UserProfile UserProfile, UserProfile UserProfileBefore) : ResponseEventBase;
