using Blazor.Shared.Settings.Components;
using Blazor.Shared.Settings.Extensions;
using Bunit;
using Xunit;

namespace Blazor.Shared.Tests.Settings;

public class SettingsPopupTests
{
    [Fact]
    public void ComponentGetsRendered()
    {
        // Arrange
        using var ctx = new TestContext();
        ctx.SetupBlazorSharedSettings(setup => setup.Services.AddSettingsPopup());

        // Act
        var component = ctx.RenderComponent<SettingsPopup>();

        // Assert
        Assert.NotNull(component);
    }
}
