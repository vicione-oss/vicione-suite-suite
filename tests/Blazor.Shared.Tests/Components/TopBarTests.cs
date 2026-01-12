using Blazor.Shared.Components;
using Bunit;
using Sdk.Testing.Client;
using Xunit;

namespace Blazor.Shared.Tests.Components;

public sealed class TopBarTests
{
    [Fact]
    public void ComponentGetsRendered()
    {
        // Arrange
        using var ctx = new TestContext();
        ctx.SetupSuiteServices();
        ctx.SetLocalServices();

        // Act + Assert
        Assert.NotNull(ctx.RenderComponent<TopBar>());
    }

    [Fact]
    public void RootNavigationInvoked()
    {
        // Arrange
        using var ctx = new TestContext();
        ctx.SetupSuiteServices();
        ctx.SetLocalServices();

        // Act + Assert
        var component = ctx.RenderComponent<TopBar>();

        var link1 = component.Find(".top-bar-app-menu");
        Assert.NotNull(link1);
        link1.Click();
    }
}
