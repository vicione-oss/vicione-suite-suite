using Burger.Internal.Contracts;
using Microsoft.Extensions.Logging;

namespace Burger.Backend.Services;

public sealed partial class Grill(ILogger<Grill> logger) : IGrill
{
    private readonly ILogger<Grill> _logger = logger;
    private readonly HashSet<BurgerPatty> _patties = [];

    public async Task<BurgerPatty> CookOrUseExistingPatty(decimal weight, bool cheese)
    {
        var existing = _patties.FirstOrDefault(x => x.Cheese == cheese && x.Weight == weight);
        if (existing is not null)
        {
            LogUsingExistingPatty(_logger, existing.Weight, existing.Cheese);

            _patties.Remove(existing);
            return existing;
        }

        LogGrillingPatty(_logger, weight, cheese);

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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Using existing patty {Weight} {Cheese}")]
    private static partial void LogUsingExistingPatty(ILogger logger, decimal weight, bool cheese);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Grilling patty {Weight} {Cheese}")]
    private static partial void LogGrillingPatty(ILogger logger, decimal weight, bool cheese);
}
