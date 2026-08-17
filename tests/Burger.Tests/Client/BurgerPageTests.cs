using Blazor.Tests.Tools;
using Bunit;
using Burger.Client;
using Burger.Client.Pages;
using Burger.Client.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Services;
using Sdk.Testing.Client;

namespace Burger.Tests.Client;

public sealed class BurgerPageTests
{
    [Fact]
    public async Task PageGetsRendered()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.SetupBlazorUiComponents(setup =>
        {
            setup.Services.AddLocalization<BurgerClientModule>();
            setup.Services.AddSingleton<OrderBurgerService>();
            setup.Services.AddSingleton(Substitute.For<IJsInterop>());
        });

        // Act
        var sut = ctx.Render<BurgerPage>();

        // Assert
        Assert.NotNull(sut);
    }
}
