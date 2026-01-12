using Blazor.Shared.NavTiles.Extensions;
using Burger.Client;
using Burger.Client.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.NavTiles.Services;
using Sdk.Testing.Client;
using Xunit;

namespace Burger.Tests.Client;

public class BurgerModuleTests
{
    [Fact]
    public void Init_module_should_register_and_configure_services()
    {
        // Arrange
        var module = new BurgerClientModule();

        // Act
        var serviceProvider = module.TestModuleInitialization(null);

        // Assert
        Assert.NotNull(serviceProvider.GetRequiredService<OrderBurgerService>());
    }

    [Fact]
    public void Should_register_nav_tile()
    {
        // Arrange
        var module = new BurgerClientModule();

        // Act
        var serviceProvider = module.TestModuleInitialization(setup =>
        {
            setup.Services.AddNavTilesInfrastructure();
        });
        var navTileRegistry = serviceProvider.GetRequiredService<INavTileRegistry<BurgerClientModule>>();

        // Assert
        Assert.NotNull(navTileRegistry);
        Assert.NotNull(navTileRegistry.FirstOrDefault());
    }
}
