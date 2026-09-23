using Burger.Internal.Contracts;
using Burger.Public.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Burger.Backend.Activities;

public sealed class DressBurgerActivity(ILogger<DressBurgerActivity> logger) : IExecuteActivity<DressBurgerArguments>
{
    private readonly ILogger<DressBurgerActivity> _logger = logger;

    public async Task<ExecutionResult> Execute(ExecuteContext<DressBurgerArguments> context)
    {
        var nameofPatty = nameof(IGrillBurgerLog.Patty);

        // The grill activity completed with variables (key: patty, value: BurgerPatty), so the patty
        // is read back from the routing slip context to continue with dressing.
        var patty = context.GetVariable<BurgerPatty>(nameof(IGrillBurgerLog.Patty)) ??
            throw new ArgumentNullException(nameofPatty);

        var arguments = context.Arguments;

        _logger.LogDebug("Dressing burger for order:{OrderId} paddy:{Weight} lettuce:{Lettuce}", arguments.OrderId, patty.Weight,
            arguments.Lettuce);

        if (arguments.Lettuce)
            throw new InvalidOperationException("No lettuce available");

        if (arguments.OnionRing)
        {
            // An activity that needs something from another service asks for it here, with an
            // IRequestClient<T> and an awaited GetResponse call. The onion ring side is left out so
            // the sample stays focused on the routing slip.
        }

        // Dressing takes some time too.
        await Task.Delay(2000);

        var burger = new SuiteBurger
        {
            BurgerId = arguments.BurgerId,
            Weight = patty.Weight,
            Cheese = patty.Cheese,
            Lettuce = arguments.Lettuce,
            Onion = arguments.Onion,
            Pickle = arguments.Pickle,
            Ketchup = arguments.Ketchup,
            Mustard = arguments.Mustard,
            BarbecueSauce = arguments.BarbecueSauce,
            OnionRing = arguments.OnionRing
        };

        return context.CompletedWithVariables(new { context.Arguments.OrderId, burger });
    }
}
