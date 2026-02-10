using Bunit;
using Sdk.Client.Components.Layout;
using Sdk.Testing.Client;
using Xunit;

namespace Blazor.Shared.Tests.Components;

public class SplitViewTests
{
    [Fact]
    public async Task ComponentGetsRendered()
    {
        // Arrange        
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServices();

        // Act + Assert
        Assert.NotNull(ctx.Render<SplitViewComponent>());
    }
}
