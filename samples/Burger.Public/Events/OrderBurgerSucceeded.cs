using Sdk.Messaging;

namespace Burger.Public.Events;

[ForwardToUI]
public sealed record OrderBurgerSucceeded(Guid OrderId, string Description) : IEvent;
