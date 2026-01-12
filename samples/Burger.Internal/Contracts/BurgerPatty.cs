namespace Burger.Internal.Contracts;

public sealed record BurgerPatty
{
    public decimal Weight { get; init; }
    public bool Cheese { get; init; }
}
