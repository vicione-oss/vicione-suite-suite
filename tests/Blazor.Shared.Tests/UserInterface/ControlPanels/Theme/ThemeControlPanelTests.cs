using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.UserInterface.ControlPanels.Theme.Components;
using Blazor.Shared.UserInterface.ControlPanels.Theme.Extensions;
using Blazor.Shared.UserInterface.ControlPanels.Theme.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Services;
using Sdk.Testing.Client;

namespace Blazor.Shared.Tests.UserInterface.ControlPanels.Theme;

public sealed class ThemeControlPanelTests
{
    [Fact]
    public void Should_render_component()
    {
        // Arrange
        using var ctx = new BunitContext();

        var dropDownModule = ctx.JSInterop.SetupModule("./_content/ViciOne.Ui.Blazor.Components/drop-down/drop-down.js");
        var dropDownInstance = dropDownModule.SetupModule("attach", _ => true);
        dropDownInstance.SetupVoid("attachInputElement", _ => true);
        dropDownInstance.SetupVoid("setMinimumWidth");

        ctx.SetupSuiteServices(setup =>
        {
            setup.Services.AddControlPanelInfrastructure();
            setup.Services.AddThemeControlPanel();
        });

        var state = new ThemeControlPanelState();

        var registry = ctx.Services.GetRequiredService<IControlPanelRegistry<SharedClientModule>>();
        var registryItem = registry.First();

        // Act
        var component = ctx.Render<ThemeControlPanel>(builder => builder
            .Add(c => c.State, state)
            .AddCascadingValue(registryItem));

        // Assert
        component.Should().NotBeNull();
    }
}
