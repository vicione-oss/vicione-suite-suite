using Burger.Backend;
using Burger.Backend.DbContext;
using Burger.Backend.StateMachines;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Connections.Contracts;
using Sdk.Testing.Backend;
using Xunit;

namespace Burger.Tests.Backend;

public class BurgerBackendModuleTests
{
    [Fact]
    public void Init_module_should_register_and_configure_services()
    {
        // Arrange
        var module = new BurgerBackendModule();

        // Act
        var serviceProvider = module.TestSagaModuleInitialization(s =>
            {
                s.AddSingleton(_ => Substitute.For<IConnectionTypeRegistry>());
            },
            null,
            busMock =>
            {
                busMock.AddSagaStateMachine<OrderBurgerStateMachine, OrderBurgerState>(null)
                    .Returns(Substitute.For<ISagaRegistrationConfigurator<OrderBurgerState>>());
            });

        // Assert
        Assert.NotNull(module.ModuleKey.ModuleId);
        Assert.NotNull(serviceProvider.GetRequiredService<IBurgerDbContext>());
    }
}
