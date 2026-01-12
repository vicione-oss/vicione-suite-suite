using Sdk.Messaging;

namespace Burger.Public.Events;

[ForwardToUI]
public sealed record OrderBurgerFailed(Guid OrderId) : IEvent;
