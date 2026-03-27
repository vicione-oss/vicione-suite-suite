using Core.Shared.UserManagement.Contracts;
using Sdk.Messaging;

namespace Core.Shared.UserManagement.Events;

[ForwardToUI]
public sealed record UserCreatedEvent(UserProfile UserProfile) : ResponseEventBase;
