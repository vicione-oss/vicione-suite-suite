using Burger.Public.Contracts;
using Sdk.Client.Connections;

namespace Burger.Client.Connections;

public class BurgerItemValidator : ItemValidatorBase<BurgerConnection>
{
    protected override void ValidateInternal(BurgerConnection item)
    {
        if (item.Patties <= 0)
            AddError(nameof(BurgerConnection.Patties), "The number of patties must be greater than zero.");
        else if (item.Patties > 2)
            AddError(nameof(BurgerConnection.Patties), "The number of patties cannot exceed two.");

        if (string.IsNullOrWhiteSpace(item.PattyType))
            AddError(nameof(BurgerConnection.PattyType), "The patty type must not be empty.");
        else if (item.PattyType.Length > 50)
            AddError(nameof(BurgerConnection.PattyType), "The patty type must not exceed 50 characters.");

        if (string.IsNullOrWhiteSpace(item.BunType))
            AddError(nameof(BurgerConnection.BunType), "The bun type must not be empty.");
        else if (item.BunType.Length > 50)
            AddError(nameof(BurgerConnection.BunType), "The bun type must not exceed 50 characters.");
    }
}
