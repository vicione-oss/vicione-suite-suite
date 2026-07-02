using Burger.Internal;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace Burger.Backend.Activities;

[MessageEndpoint(Endpoints.GrillBurgerActivity)]
public sealed record GrillBurgerArguments : IActivityArgument
{
    public Guid OrderId { get; init; }
    public Guid BurgerId { get; init; }

    public decimal Weight { get; init; }
    public bool Cheese { get; init; }
}
