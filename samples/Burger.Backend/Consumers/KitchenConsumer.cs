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
/// The kitchen: consumes a kitchen command and produces burgers.
/// </summary>
public sealed partial class KitchenConsumer(IRoutingSlipBuilderFactory factory, ILogger<KitchenConsumer> logger) :
    TrackingConsumerBase, IConsumer<KitchenCommand>
{
    private readonly Random _random = new();

    public async Task Consume(ConsumeContext<KitchenCommand> context)
    {
        LogConsume(logger, nameof(KitchenCommand), context.CorrelationId, context.Message.OrderId, context.Message.Burgers.Count);

        // todo: there's a list of burgers to be parallelized
        if (context.Message.Burgers.Count == 0)
            return;

        var builder = factory.Create(context.CorrelationId ?? Guid.NewGuid());

        builder.AddActivity<GrillBurgerActivity, GrillBurgerArguments>(CreateGrillArguments(context.Message.OrderId, context.Message.Burgers.First()));

        builder.AddActivity<DressBurgerActivity, DressBurgerArguments>(CreateDressArguments(context.Message.OrderId, context.Message.Burgers.First()));

        // Executing the routing slip raises RoutingSlipCompleted or RoutingSlipFaulted.
        await ExecuteTracked(context, builder).ConfigureAwait(false);
    }

    protected override async Task ConsumeCompleted(ConsumeContext<RoutingSlipCompleted> context)
    {
        // The order id comes from the activity variables, which is what correlates the
        // BurgerCompleted event back to the saga.
        var orderId = context.GetVariable<Guid>(nameof(KitchenCommand.OrderId));
        if (!orderId.HasValue)
        {
            LogMissingOrderId(logger);
            return;
        }

        // todo: check for a better/safer way to handle the variables
        var burger = context.GetVariable<SuiteBurger>("Burger");
        if (burger is null)
        {
            LogNoBurgerAvailable(logger);
            return;
        }

        // Publishing the event advances the saga.
        await context.Publish(new BurgerCompleted
        {
            OrderId = orderId.Value,
            Burgers = [burger]
        })
            .ConfigureAwait(false);

        LogKitchenActivitiesCompleted(logger, orderId.Value, context.Message.TrackingNumber);
    }

    protected override Task ConsumeFaulted(ConsumeContext<RoutingSlipFaulted> context)
    {
        var errors = context.Message.ActivityExceptions.Select(k => k.ExceptionInfo.Message);
        LogConsumeFailed(logger, nameof(KitchenCommand), context.CorrelationId, errors);

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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consume {Command} CorrelationId:{CorrelationId} OrderId:{OrderId} Burgers.Count:{Count}")]
    private static partial void LogConsume(ILogger logger, string command, Guid? correlationId, Guid orderId, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Missing order id - what to do?!")]
    private static partial void LogMissingOrderId(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kitchen activities completed but no burger is available - what to do?!")]
    private static partial void LogNoBurgerAvailable(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "KitchenActivities completed. OrderId:{OrderId} TrackingId: {TrackingId}")]
    private static partial void LogKitchenActivitiesCompleted(ILogger logger, Guid orderId, Guid trackingId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Consume {Command} CorrelationId:{CorrelationId} failed with {Errors}")]
    private static partial void LogConsumeFailed(ILogger logger, string command, Guid? correlationId, IEnumerable<string> errors);
}
