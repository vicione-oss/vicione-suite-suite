using Blazor.Tests.Tools;
using Bunit;
using Burger.Client;
using Burger.Client.Pages;
using Burger.Client.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Testing.Client;
using Xunit;

namespace Burger.Tests.Client;

public sealed class BurgerPageTests
{
    [Fact]
    public void PageGetsRendered()
    {
        // Arrange
        using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx(setup =>
        {
            setup.Services.AddLocalization<BurgerClientModule>();
        });
        ctx.Services.AddSingleton<OrderBurgerService>();

        // Act
        var sut = ctx.Render<BurgerPage>();

        // Assert
        Assert.NotNull(sut);
    }
}
