using System.Data;
using Burger.Backend.Consumers;
using Burger.Backend.DbContext;
using Burger.Backend.StateMachines;
using Burger.Internal.Events;
using Burger.Public.Commands;
using Burger.Public.Contracts;
using Burger.Public.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Testing.Backend;
using Xunit;

namespace Burger.Tests.Backend;

public class OrderBurgerStateMachineTests : TestWithDbContextSqlite<BurgerDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public OrderBurgerStateMachineTests()
    {
        _configureServices = cfg =>
        {
            cfg.AddSingleton(_ => TestDbContext);
            // saga needs
            cfg.AddSagaStateMachine<OrderBurgerStateMachine, OrderBurgerState>()
                .EntityFrameworkRepository(r =>
                {
                    r.IsolationLevel = IsolationLevel.Serializable;
                    r.DatabaseFactory(s => () => (BurgerDbContext)s.GetRequiredService<IBurgerDbContext>());
                    r.ConcurrencyMode = ConcurrencyMode.Optimistic; // or use Optimistic, which requires RowVersion
                })
                .InMemoryRepository();
        };
    }

    [Fact]
    public async Task Command_should_be_consumed()
    {
        await using var tester = new MassTransitTester(_configureServices);
        // Arrange
        var orderId = Guid.NewGuid();
        var burgers = new List<SuiteBurger> { new() { BurgerId = Guid.NewGuid() } };
        var command = new OrderBurger(orderId, burgers);

        await tester.Harness.Start();
        var sagaHarness = tester.Harness.GetSagaStateMachineHarness<OrderBurgerStateMachine, OrderBurgerState>();

        // Act - Order
        await tester.Harness.Bus.Publish(command);

        Assert.True(await sagaHarness.Consumed.Any<OrderBurger>());
        Assert.True(await sagaHarness.Created.Any(x => x.CorrelationId == orderId));
        Assert.True(await tester.Harness.Published.Any<OrderBurgerReceived>());
        Assert.True(await tester.Harness.Sent.Any<KitchenCommand>());

        // Act - Cook
        var orderCompleted = new BurgerCompleted
        {
            OrderId = orderId,
            Burgers = burgers
        };
        await tester.Harness.Bus.Publish(orderCompleted);

        Assert.True(await sagaHarness.Consumed.Any<BurgerCompleted>());
        Assert.True(await tester.Harness.Sent.Any<DeliveryCommand>());

        // Act - Deliver
        var burgerDelivered = new DeliveryCompleted(orderId);
        await tester.Harness.Bus.Publish(burgerDelivered);

        Assert.True(await sagaHarness.Consumed.Any<DeliveryCompleted>());
        Assert.True(await tester.Harness.Published.Any<OrderBurgerSucceeded>());
    }
}
