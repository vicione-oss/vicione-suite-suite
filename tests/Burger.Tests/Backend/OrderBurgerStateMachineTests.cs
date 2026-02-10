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
        await tester.Harness.Bus.Publish(command, TestContext.Current.CancellationToken);

        Assert.True(await sagaHarness.Consumed.Any<OrderBurger>(TestContext.Current.CancellationToken));
        Assert.True(await sagaHarness.Created.Any(x => x.CorrelationId == orderId, TestContext.Current.CancellationToken));
        Assert.True(await tester.Harness.Published.Any<OrderBurgerReceived>(TestContext.Current.CancellationToken));
        Assert.True(await tester.Harness.Sent.Any<KitchenCommand>(TestContext.Current.CancellationToken));

        // Act - Cook
        var orderCompleted = new BurgerCompleted
        {
            OrderId = orderId,
            Burgers = burgers
        };
        await tester.Harness.Bus.Publish(orderCompleted, TestContext.Current.CancellationToken);

        Assert.True(await sagaHarness.Consumed.Any<BurgerCompleted>(TestContext.Current.CancellationToken));
        Assert.True(await tester.Harness.Sent.Any<DeliveryCommand>(TestContext.Current.CancellationToken));

        // Act - Deliver
        var burgerDelivered = new DeliveryCompleted(orderId);
        await tester.Harness.Bus.Publish(burgerDelivered, TestContext.Current.CancellationToken);

        Assert.True(await sagaHarness.Consumed.Any<DeliveryCompleted>(TestContext.Current.CancellationToken));
        Assert.True(await tester.Harness.Published.Any<OrderBurgerSucceeded>(TestContext.Current.CancellationToken));
    }
}
