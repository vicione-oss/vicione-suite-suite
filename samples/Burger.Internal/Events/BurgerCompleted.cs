using Burger.Public.Contracts;
using MassTransit;
using Sdk.Messaging;

namespace Burger.Internal.Events;

public sealed record BurgerCompleted : IEvent, CorrelatedBy<Guid>
{
    public List<SuiteBurger> Burgers { get; init; } = [];
    public Guid OrderId { get; init; }
    public Guid CorrelationId => OrderId;
}
