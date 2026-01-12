using Blazor.Shared.UserInterface.ControlPanels.Theme.Components;
using Blazor.Shared.UserInterface.ControlPanels.Theme.Extensions;
using Blazor.Shared.UserInterface.ControlPanels.Theme.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Services;
using Sdk.Testing.Client;
using Xunit;

namespace Blazor.Shared.Tests.UserInterface.ControlPanels.Theme;

public sealed class ThemeControlPanelTests
{
    [Fact]
    public void Component_should_render()
    {
        // Arrange
        using var ctx = new TestContext();

        ctx.SetupSuiteServices(setup =>
        {
            setup.Services.AddThemeControlPanel();

            setup.Services.AddScoped(_ => Substitute.For<IActiveControlPanelPageProvider>());
        });

        var state = new ThemeControlPanelState();

        // Act
        var component = ctx.RenderComponent<ThemeControlPanel>(p => p.Add(c => c.State, state));

        // Assert
        Assert.NotNull(component);
    }
}
