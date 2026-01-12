using Burger.Public.Contracts;
using Sdk.Messaging;

namespace Burger.Public.Commands;

[MessageEndpoint("OrderBurgerState")]
public sealed record OrderBurger(Guid OrderId, List<SuiteBurger> Burgers) : ICommand
{
    public Guid CorrelationId => OrderId;
}
