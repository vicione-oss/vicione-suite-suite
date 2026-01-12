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

        // the grill activity completed with variables -> key:patty val:BurgerPatty so we
        // get it from context to continue with dressing
        var patty = context.GetVariable<BurgerPatty>(nameof(IGrillBurgerLog.Patty)) ??
            throw new ArgumentNullException(nameofPatty);

        var arguments = context.Arguments;

        _logger.LogDebug("Dressing burger for order:{OrderId} paddy:{Weight} lettuce:{Lettuce}", arguments.OrderId, patty.Weight,
            arguments.Lettuce);

        if (arguments.Lettuce)
            throw new InvalidOperationException("No lettuce available");

        if (arguments.OnionRing)
        {
            // Guid? onionRingId = arguments.OnionRingId ?? NewId.NextGuid();
            //
            // _logger.LogDebug("Ordering Onion Ring: {OrderId}", onionRingId);

            // Response<OnionRingsCompleted> response = await _onionRingClient.GetResponse<OnionRingsCompleted>(new
            // {
            //     arguments.OrderId,
            //     OrderLineId = onionRingId,
            //     Quantity = 1
            // }, context.CancellationToken);
        }

        // dressing takes some time too
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
