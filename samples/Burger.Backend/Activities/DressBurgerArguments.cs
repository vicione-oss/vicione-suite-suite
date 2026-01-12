using Burger.Internal;
using Sdk.Messaging;

namespace Burger.Backend.Activities;

[MessageEndpoint(Endpoints.DressBurgerActivity)]
public sealed record DressBurgerArguments : IActivityArgument
{
    public Guid OrderId { get; init; }
    public Guid BurgerId { get; init; }

    /// <summary>
    /// this will trigger an exception in the DressBurgerActivity and should lead to
    /// compensate grill activity
    /// </summary>
    public bool Lettuce { get; init; }
    public bool Pickle { get; set; }
    public bool Onion { get; set; }
    public bool Ketchup { get; set; }
    public bool Mustard { get; set; }
    public bool BarbecueSauce { get; set; }
    public bool OnionRing { get; set; }
}
