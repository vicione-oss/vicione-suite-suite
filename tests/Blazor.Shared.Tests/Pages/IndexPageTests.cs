using AwesomeAssertions;
using Blazor.Shared.Components.Pages;
using Blazor.Shared.NavTiles.Extensions;
using Bunit;
using Sdk.Client.Modules;
using Sdk.Client.NavTiles.Extensions;
using Sdk.Testing.Client;
using TestModule.Client;
using Xunit;

namespace Blazor.Shared.Tests.Pages;

public sealed class IndexPageTests
{
    [Fact]
    public void Should_render_nav_items()
    {
        // Arrange
        using var ctx = new BunitContext();

        ctx.SetupSuiteServices(setup =>
        {
            setup.Services.AddNavTilesInfrastructure();
            setup.Services.AddNavTiles<TestClientModule>();
            setup.Services.AddNavTiles<TestOtherEditorClientModule>();

            setup.FakeAuthenticationStateProvider = true;
        });

        // Act
        var component = ctx.Render<IndexPage>();

        // Assert
        var tileGroups = component.FindAll(".nav-tile-panel");
        tileGroups.Should().HaveCount(2);

        var navTiles = component.FindAll(".nav-tile-container");
        navTiles.Should().HaveCount(2);
    }

    [Fact]
    public void Should_render_nav_tiles()
    {
        // Arrange
        var clientModules = new List<ClientModule>
        {
            new TestClientModule(),
            new TestSomeEditorClientModule(),
            new TestOtherEditorClientModule(),
        };

        using var ctx = new BunitContext();
        ctx.SetupSuiteServices(setup =>
        {
            setup.Services.AddNavTilesInfrastructure();

            setup.FakeAuthenticationStateProvider = true;
        });

        foreach (var clientModule in clientModules)
        {
            if (clientModule.Configure is not null)
                clientModule.Configure(ctx.Services);
        }


        // Act
        var component = ctx.Render<IndexPage>();

        // Assert
        var navTileContainers = component.FindAll(".nav-tile-container");
        navTileContainers.Should().HaveCount(clientModules.Count);

        var navTileContent = component.FindAll(".nav-tile-standard-content");
        navTileContent.Should().HaveCount(clientModules.Count);
    }
}
