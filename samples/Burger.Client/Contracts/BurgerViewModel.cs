using Burger.Public.Contracts;

namespace Burger.Client.Contracts;

public sealed class BurgerViewModel
{
    public bool Lettuce { get; set; }
    public bool Cheese { get; set; }
    public bool Pickle { get; set; } = true;
    public bool Onion { get; set; } = true;
    public bool Ketchup { get; set; }
    public bool Mustard { get; set; } = true;

    public bool BarbecueSauce { get; set; }

    public SuiteBurger ToSuiteBurger() => new()
    {
        BurgerId = Guid.NewGuid(),
        Lettuce = Lettuce,
        Cheese = Cheese,
        Pickle = Pickle,
        Onion = Onion,
        Ketchup = Ketchup,
        Mustard = Mustard,
        BarbecueSauce = BarbecueSauce
    };
}
