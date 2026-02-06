using AwesomeAssertions;
using Blazor.Shared.Popup.Services;
using Blazor.Shared.Settings.Components;
using Blazor.Shared.Settings.Models;
using Blazor.Shared.Settings.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Services;
using Xunit;

namespace Blazor.Shared.Tests.Components.Layout;

public sealed class PopupMasterDetailLayoutTests
{
    [Theory]
    [InlineData(".detail__separator")]
    [InlineData(".detail__action-buttons")]
    public async Task Assert_element_exists_when_control_panel_edit_is_running(string expectedCssClass)
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.SetupBlazorSharedSettings(setup =>
        {
            setup.Services.AddScoped<IControlPanelEditRegistry, ControlPanelEditRegistry>();
            setup.Services.AddScoped(_ => Substitute.For<IActiveControlPanelPageProvider>());
            setup.Services.AddScoped(_ => Substitute.For<IControlPanelPageRegistry>());
            setup.Services.AddScoped(_ => Substitute.For<IControlPanelRequest>());
            setup.Services.AddScoped(_ => Substitute.For<INavigateBackRequest>());

            setup.FakeAuthenticationStateProvider = true;
            setup.Services.AddScoped(_ => Substitute.For<IControlPanelRegistryItemCache>());

            setup.Services.AddScoped(_ => Substitute.For<ILoadingIndicationPlacementBehavior>());
        });

        var controlPanelState = new ControlPanelState();
        await using var controlPanelEdit = new ControlPanelEdit<ControlPanelState>(controlPanelState, ctx.Services);

        var controlPanelEditRegistry = ctx.Services.GetRequiredService<IControlPanelEditRegistry>();
        controlPanelEditRegistry.Add(controlPanelEdit);

        var component = ctx.Render<SettingsContainer>();

        // Act
        controlPanelEdit.Begin();

        component.Render();

        // Assert
        component.FindAll(expectedCssClass).Should().HaveCount(1);
    }
}
