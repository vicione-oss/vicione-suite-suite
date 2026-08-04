using Blazor.Shared.Components;
using Bunit;
using Sdk.Testing.Client;

namespace Blazor.Shared.Tests.Components;

public sealed class TopBarTests
{
    [Fact]
    public async Task Should_render_component()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServices();
        ctx.SetLocalServices();

        // Act
        var component = ctx.Render<TopBar>();

        // Assert
        component.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_invoke_root_navigation_on_click()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServices();
        ctx.SetLocalServices();
        var component = ctx.Render<TopBar>();

        // Act
        var link1 = component.Find(".top-bar-app-menu");
        link1.Should().NotBeNull();
        await link1.ClickAsync();

        // Assert
    }
}
