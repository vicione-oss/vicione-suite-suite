using Burger.Backend.Consumers;
using Burger.Internal.Events;
using Burger.Public.Commands;
using Burger.Public.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;

namespace Burger.Backend.StateMachines;

/// <summary>
/// StateMachine to handle ordering a burger correlated by the OrderId
/// https://masstransit-project.com/usage/messages.html#correlation
/// </summary>
// ReSharper disable UnassignedGetOnlyAutoProperty
// ReSharper disable MemberCanBePrivate.Global
public sealed class OrderBurgerStateMachine : MassTransitStateMachine<OrderBurgerState>
{
    private readonly ILogger<OrderBurgerStateMachine> _logger;

    public State? Ordered { get; }
    public State? Cooking { get; }
    public State? Delivery { get; }
    public Event<OrderBurger>? BurgerOrdered { get; }
    public Event<BurgerCompleted>? BurgerCompleted { get; }
    public Event<DeliveryCompleted>? BurgerDelivered { get; }

    public OrderBurgerStateMachine(ILogger<OrderBurgerStateMachine> logger)
    {
        _logger = logger;

        Event(() => BurgerOrdered);
        Event(() => BurgerCompleted);
        Event(() => BurgerDelivered);

        // This results in the following values: 0 - None, 1 - Initial, 2 - Final, 3 - Ordered, 4 - Delivery
        InstanceState(x => x.CurrentState,
            Ordered,
            Cooking,
            Delivery);

        // Runs when an OrderBurger command reaches the saga queue (Ping_OrderBurgerState).
        Initially(
            When(BurgerOrdered)
                .Then(InitiallyWhenBurgerOrdered)
                .Publish(ctx => new OrderBurgerReceived(ctx.Message.OrderId))
                // Sends the order details to the kitchen, which starts the grill and dress activities.
                .Send(MessagingHelper.GetCommandEndpointAddress<KitchenCommand>(),
                    ctx => new KitchenCommand(ctx.Saga.OrderId, ctx.Saga.Burgers))
                .Then(ctx => LogTransition(ctx, nameof(Cooking)))
                .TransitionTo(Cooking)
        );

        During(Cooking,
            When(BurgerOrdered)
                .Then(context =>
                {
                    logger.LogWarning("OrderBurgerStateMachine OrderId: {OrderId} (duplicate request)", context.Message.OrderId);
                }),
            // The kitchen has grilled a patty and dressed the burger.
            When(BurgerCompleted)
                .Then(CookingWhenBurgerCompleted)
                // Sends the command that starts delivery of the ordered burgers.
                .Send(MessagingHelper.GetCommandEndpointAddress<DeliveryCommand>(), CreateDeliveryCommand)
                .Then(ctx => LogTransition(ctx, nameof(Delivery)))
                .TransitionTo(Delivery));

        // Delivery succeeds when the DeliveryCompleted event arrives.
        During(Delivery,
            When(BurgerDelivered)
                .Then(DeliveryWhenBurgerDelivered)
                .Publish(CreateOrderBurgerSucceeded)
                .Then(ctx => LogTransition(ctx, nameof(Final)))
                .Finalize());

        SetCompletedWhenFinalized();
    }

    private void InitiallyWhenBurgerOrdered(BehaviorContext<OrderBurgerState, OrderBurger> context)
    {
        _logger.LogInformation(
            "Starting burger production for order {OrderId}",
            context.Message.OrderId);

        context.Saga.CorrelationId = context.Message.OrderId;
        context.Saga.OrderId = context.Message.OrderId;
        context.Saga.Burgers.Clear();
        context.Saga.Burgers.AddRange(context.Message.Burgers);
    }

    private void CookingWhenBurgerCompleted(BehaviorContext<OrderBurgerState, BurgerCompleted> context)
    {
        _logger.LogInformation("Delivered order:{Id} {Burger}", context.Saga.CorrelationId, context.Saga.Burgers);

        // Updates the saga with the burgers from the kitchen.
        context.Saga.Burgers.Clear();
        context.Saga.Burgers.AddRange(context.Message.Burgers);
    }

    private void DeliveryWhenBurgerDelivered(BehaviorContext<OrderBurgerState> context)
    {
        _logger.LogInformation("Delivered order:{Id} {Burger}", context.Saga.CorrelationId, context.Saga.Burgers);
    }

    private DeliveryCommand CreateDeliveryCommand(BehaviorContext<OrderBurgerState, BurgerCompleted> ctx)
        => new(ctx.Saga.OrderId, ctx.Saga.Burgers);

    private OrderBurgerSucceeded CreateOrderBurgerSucceeded(BehaviorContext<OrderBurgerState, DeliveryCompleted> ctx)
        => new(ctx.Saga.OrderId, string.Join(", ", ctx.Saga.Burgers.Select(b => b.ToString())));

    private void LogTransition<TMessage>(BehaviorContext<OrderBurgerState, TMessage> ctx, string targetTransition) where TMessage : class
        => _logger.LogInformation("Saga[{CorrelationId}] transition from {OldState} to {State}",
            ctx.Saga.CorrelationId,
            GetState(ctx.Saga.CurrentState),
            targetTransition);

    private static string GetState(int sagaState)
    {
        return sagaState switch
        {
            0 => "None",
            1 => "Initial",
            2 => "Final",
            3 => nameof(Ordered),
            4 => nameof(Cooking),
            5 => nameof(Delivery),
            _ => "Unknown"
        };
    }
}
