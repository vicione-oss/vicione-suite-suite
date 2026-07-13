using AwesomeAssertions;
using Blazor.Shared.Settings.Components;
using Blazor.Shared.Settings.Extensions;
using Bunit;
using Xunit;

namespace Blazor.Shared.Tests.Settings;

public sealed class SettingsPopupTests
{
    [Fact]
    public async Task Should_render_component()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.SetupBlazorSharedSettings(setup => setup.Services
            .AddControlPanelInfrastructure()
            .AddSettingsPopup());

        // Act
        var component = ctx.Render<SettingsPopup>();

        // Assert
        component.Should().NotBeNull();
    }
}
