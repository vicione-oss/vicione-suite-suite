using Blazor.Shared.Components;
using Bunit;
using Sdk.Testing.Client;
using Xunit;

namespace Blazor.Shared.Tests.Components;

public sealed class TopBarTests
{
    [Fact]
    public async Task ComponentGetsRendered()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServices();
        ctx.SetLocalServices();

        // Act + Assert
        Assert.NotNull(ctx.Render<TopBar>());
    }

    [Fact]
    public async Task RootNavigationInvoked()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServices();
        ctx.SetLocalServices();

        // Act + Assert
        var component = ctx.Render<TopBar>();

        var link1 = component.Find(".top-bar-app-menu");
        Assert.NotNull(link1);
        link1.Click();
    }
}
