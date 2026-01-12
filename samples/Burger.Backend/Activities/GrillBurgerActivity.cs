using Burger.Backend.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Burger.Backend.Activities;

public sealed class GrillBurgerActivity(ILogger<GrillBurgerActivity> logger, IGrill grill) :
    IActivity<GrillBurgerArguments, IGrillBurgerLog>
{
    private readonly IGrill _grill = grill;
    private readonly ILogger<GrillBurgerActivity> _logger = logger;

    public async Task<ExecutionResult> Execute(ExecuteContext<GrillBurgerArguments> context)
    {
        var patty = await _grill.CookOrUseExistingPatty(context.Arguments.Weight, context.Arguments.Cheese);

        return context.CompletedWithVariables<IGrillBurgerLog>(new { patty }, new { patty });
    }

    public Task<CompensationResult> Compensate(CompensateContext<IGrillBurgerLog> context)
    {
        var patty = context.Log.Patty;

        _logger.LogDebug("Putting Burger back in inventory: {Weight} {Cheese}", patty.Weight, patty.Cheese);

        _grill.Add(patty);

        return Task.FromResult(context.Compensated());
    }
}
