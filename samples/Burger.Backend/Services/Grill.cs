using Burger.Internal.Contracts;
using Microsoft.Extensions.Logging;

namespace Burger.Backend.Services;

public sealed class Grill(ILogger<Grill> logger) : IGrill
{
    private readonly ILogger<Grill> _logger = logger;
    private readonly HashSet<BurgerPatty> _patties = [];

    public async Task<BurgerPatty> CookOrUseExistingPatty(decimal weight, bool cheese)
    {
        var existing = _patties.FirstOrDefault(x => x.Cheese == cheese && x.Weight == weight);
        if (existing is not null)
        {
            _logger.LogDebug("Using existing patty {Weight} {Cheese}", existing.Weight, existing.Cheese);

            _patties.Remove(existing);
            return existing;
        }

        _logger.LogDebug("Grilling patty {Weight} {Cheese}", weight, cheese);

        await Task.Delay(5000 + (int)(1000.0m * weight));

        var patty = new BurgerPatty
        {
            Weight = weight,
            Cheese = cheese
        };

        return patty;
    }

    public void Add(BurgerPatty patty)
    {
        _patties.Add(patty);
    }
}
