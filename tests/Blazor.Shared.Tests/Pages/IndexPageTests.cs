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

public class IndexPageTests
{
    [Fact]
    public void NavItemsGetRendered()
    {
        // Arrange
        using var ctx = new TestContext();

        ctx.SetupSuiteServices(setup =>
        {
            setup.Services.AddNavTilesInfrastructure();
            setup.Services.AddNavTiles<TestClientModule>();
            setup.Services.AddNavTiles<TestOtherEditorClientModule>();

            setup.FakeAuthenticationStateProvider = true;
        });

        // Act
        var component = ctx.RenderComponent<IndexPage>();

        // Assert
        var tileGroups = component.FindAll(".nav-tile-panel");
        Assert.Equal(2, tileGroups.Count);

        var navTiles = component.FindAll(".nav-tile-container");
        navTiles.Should().HaveCount(2);
    }

    [Fact]
    public void ShouldRenderNavTiles()
    {
        // Arrange
        var clientModules = new List<ClientModule>
        {
            new TestClientModule(),
            new TestSomeEditorClientModule(),
            new TestOtherEditorClientModule(),
        };

        using var ctx = new TestContext();
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

        // setup of jsModule.Setup<IJSObjectReference>("initSubline", _ => true); as instructed by bUnit iself throws mysterios exception,
        // using loose mode instead
        // https://github.com/bUnit-dev/bUnit/discussions/1248
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        // Act
        var component = ctx.RenderComponent<IndexPage>();

        // Assert
        var navTileContainers = component.FindAll(".nav-tile-container");
        Assert.Equal(clientModules.Count, navTileContainers.Count);

        var navTileContent = component.FindAll(".nav-tile-standard-content");
        Assert.Equal(clientModules.Count, navTileContent.Count);
    }
}
