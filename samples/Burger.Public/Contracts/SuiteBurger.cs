using System.Globalization;
using System.Text;

namespace Burger.Public.Contracts;

public sealed record SuiteBurger
{
    public Guid BurgerId { get; init; }
    public decimal Weight { get; init; } = 0.5m;
    public bool Lettuce { get; init; }
    public bool Cheese { get; init; }
    public bool Pickle { get; init; } = true;
    public bool Onion { get; init; } = true;
    public bool Ketchup { get; init; }
    public bool Mustard { get; init; } = true;
    public bool BarbecueSauce { get; init; }
    public bool OnionRing { get; init; }

    public override string ToString()
    {
        StringBuilder sb = new();

        sb.Append(CultureInfo.InvariantCulture, $"Burger: {Weight:F2} lb");

        if (Cheese)
            sb.Append(" Cheese");
        if (Lettuce)
            sb.Append(" Lettuce");
        if (Pickle)
            sb.Append(" Pickle");
        if (Onion)
            sb.Append(" Onion");
        if (Ketchup)
            sb.Append(" Ketchup");
        if (Mustard)
            sb.Append(" Mustard");
        if (BarbecueSauce)
            sb.Append(" BBQ");
        if (OnionRing)
            sb.Append(" OnionRing");

        return sb.ToString();
    }
}
