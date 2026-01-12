using Burger.Backend.Activities;
using Burger.Internal.Events;
using Burger.Public.Contracts;
using MassTransit;
using MassTransit.Courier.Contracts;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Extensions;
using Sdk.Backend.Messaging;

namespace Burger.Backend.Consumers;

/// <summary>
/// This is the kitchen that produces burgers consuming a kitchen command
/// </summary>
public sealed class KitchenConsumer(IRoutingSlipBuilderFactory factory, ILogger<KitchenConsumer> logger) :
    TrackingConsumerBase, IConsumer<KitchenCommand>
{
    private readonly Random _random = new();

    public async Task Consume(ConsumeContext<KitchenCommand> context)
    {
        logger.LogDebug("Consume {Command} CorrelationId:{CorrelationId} OrderId:{OrderId} Burgers.Count:{Count}",
            nameof(KitchenCommand),
            context.CorrelationId,
            context.Message.OrderId,
            context.Message.Burgers.Count);

        // todo: there's a list of burgers to be parallelized
        if (context.Message.Burgers.Count == 0)
            return;

        var builder = factory.Create(context.CorrelationId ?? Guid.NewGuid());

        builder.AddActivity<GrillBurgerActivity, GrillBurgerArguments>(CreateGrillArguments(context.Message.OrderId, context.Message.Burgers.First()));

        builder.AddActivity<DressBurgerActivity, DressBurgerArguments>(CreateDressArguments(context.Message.OrderId, context.Message.Burgers.First()));

        // will execute the activities -> RoutingSlipCompleted|Faulted
        await ExecuteTracked(context, builder).ConfigureAwait(false);
    }

    protected override async Task ConsumeCompleted(ConsumeContext<RoutingSlipCompleted> context)
    {
        // we need the order id from the activity variables to correlate the BurgerCompleted event
        // back to our saga - there's for sure a more elegant way to do this
        var orderId = context.GetVariable<Guid>(nameof(KitchenCommand.OrderId));
        if (!orderId.HasValue)
        {
            logger.LogWarning("Missing order id - what to do?!");
            return;
        }

        // todo: check for a better/safer way to handle the variables
        var burger = context.GetVariable<SuiteBurger>("Burger");
        if (burger is null)
        {
            logger.LogWarning("Kitchen activities completed but no burger is available - what to do?!");
            return;
        }

        // trigger state transition in our saga
        await context.Publish(new BurgerCompleted
        {
            OrderId = orderId.Value,
            Burgers = [burger]
        })
            .ConfigureAwait(false);

        logger.LogInformation("KitchenActivities completed. OrderId:{OrderId} TrackingId: {TrackingId}",
            orderId.Value,
            context.Message.TrackingNumber);
    }

    protected override Task ConsumeFaulted(ConsumeContext<RoutingSlipFaulted> context)
    {
        logger.LogError("Consume {Command} CorrelationId:{CorrelationId} failed with {}",
            nameof(KitchenCommand),
            context.CorrelationId,
            string.Join(", ", context.Message.ActivityExceptions.Select(k => k.ExceptionInfo.Message)));

        return Task.CompletedTask;
    }

    private GrillBurgerArguments CreateGrillArguments(Guid orderId, SuiteBurger burger)
        => new()
        {
            OrderId = orderId,
            BurgerId = burger.BurgerId,
            Weight = new decimal(_random.NextDouble()),
            Cheese = burger.Cheese
        };

    private static DressBurgerArguments CreateDressArguments(Guid orderId, SuiteBurger burger)
        => new()
        {
            OrderId = orderId,
            BurgerId = burger.BurgerId,
            Ketchup = burger.Ketchup,
            Mustard = burger.Mustard,
            Onion = burger.Onion,
            Pickle = burger.Pickle,
            BarbecueSauce = burger.BarbecueSauce,
            OnionRing = burger.OnionRing,
            Lettuce = burger.Lettuce
        };
}
