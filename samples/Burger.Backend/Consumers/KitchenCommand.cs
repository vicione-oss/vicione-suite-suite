using Burger.Public.Contracts;
using Sdk.Messaging;

namespace Burger.Backend.Consumers;

public sealed record KitchenCommand(Guid OrderId, List<SuiteBurger> Burgers) : ICommand
{
    public Guid CorrelationId => OrderId;
}
