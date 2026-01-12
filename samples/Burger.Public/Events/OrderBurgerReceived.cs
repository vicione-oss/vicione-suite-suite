using Sdk.Messaging;

namespace Burger.Public.Events;

[ForwardToUI]
public sealed record OrderBurgerReceived(Guid OrderId) : IEvent;
