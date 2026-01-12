using System.ComponentModel;
using System.Runtime.CompilerServices;
using Burger.Client.Contracts;
using Burger.Public.Commands;
using Burger.Public.Events;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using Sdk.Utils;

namespace Burger.Client.Services;

public sealed class OrderBurgerService : IDisposable,
    IEventConsumer<OrderBurgerReceived>,
    IEventConsumer<OrderBurgerSucceeded>,
    IEventConsumer<OrderBurgerFailed>,
    INotifyPropertyChanged
{
    private readonly IUiMediator _mediator;
    private readonly ILogger<OrderBurgerService> _logger;
    private readonly AutoDisposeList<IDisposable> _subscriptionHandle = [];

    public List<OrderViewModel> Orders { get; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public OrderBurgerService(IUiMediator mediator, ILogger<OrderBurgerService> logger)
    {
        _mediator = mediator;
        _logger = logger;

        _subscriptionHandle.Add(_mediator.Register<OrderBurgerReceived>(this));
        _subscriptionHandle.Add(_mediator.Register<OrderBurgerSucceeded>(this));
        _subscriptionHandle.Add(_mediator.Register<OrderBurgerFailed>(this));
    }

    public void Dispose() => _subscriptionHandle.Dispose();

    public async Task OrderBurger(BurgerViewModel burger)
    {
        var order = new OrderViewModel { OrderId = Guid.NewGuid() };
        var orderCommand = new OrderBurger(order.OrderId, [burger.ToSuiteBurger()]);

        // should trigger the state machines initially but does not :(
        // we would have to publish it
        await _mediator.Send(orderCommand).ConfigureAwait(false);
        Orders.Add(order);
    }

    public Task Consume(ClientContext<OrderBurgerReceived> context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Consuming<{Event}> for order {OrderId}", nameof(OrderBurgerReceived), context.Message.OrderId);

        var order = Orders.FirstOrDefault(k => k.OrderId == context.Message.OrderId);
        if (order is not null)
        {
            order.State = OrderState.Ordered;
            order.Description = "Kitchen received the order";
            OnPropertyChanged(nameof(Orders));
        }

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<OrderBurgerSucceeded> context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Consuming<{Event}> for order {OrderId}", nameof(OrderBurgerSucceeded), context.Message.OrderId);

        var order = Orders.FirstOrDefault(k => k.OrderId == context.Message.OrderId);
        if (order is not null)
        {
            order.State = OrderState.Delivered;
            order.Description = context.Message.Description;
            order.DeliveredWhen = DateTimeOffset.Now;
            OnPropertyChanged(nameof(Orders));
        }

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<OrderBurgerFailed> context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Consuming<{Event}> for order {OrderId}", nameof(OrderBurgerFailed), context.Message.OrderId);

        var order = Orders.FirstOrDefault(k => k.OrderId == context.Message.OrderId);
        if (order is not null)
        {
            order.State = OrderState.Failed;
            order.Description = "Something went wrong :(";
            OnPropertyChanged(nameof(Orders));
        }

        return Task.CompletedTask;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
