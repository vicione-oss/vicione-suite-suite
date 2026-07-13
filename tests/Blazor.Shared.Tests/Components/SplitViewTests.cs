using AwesomeAssertions;
using Bunit;
using Sdk.Client.Components.Layout;
using Sdk.Testing.Client;
using Xunit;

namespace Blazor.Shared.Tests.Components;

public class SplitViewTests
{
    [Fact]
    public async Task Should_render_component()
    {
        // Arrange        
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServices();

        // Act
        var component = ctx.Render<SplitViewComponent>();

        // Assert
        component.Should().NotBeNull();
    }
}
