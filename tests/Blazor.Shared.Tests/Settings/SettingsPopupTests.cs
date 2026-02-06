using Blazor.Shared.Settings.Components;
using Blazor.Shared.Settings.Extensions;
using Bunit;
using Xunit;

namespace Blazor.Shared.Tests.Settings;

public class SettingsPopupTests
{
    [Fact]
    public async Task ComponentGetsRendered()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.SetupBlazorSharedSettings(setup => setup.Services.AddSettingsPopup());

        // Act
        var component = ctx.Render<SettingsPopup>();

        // Assert
        Assert.NotNull(component);
    }
}
