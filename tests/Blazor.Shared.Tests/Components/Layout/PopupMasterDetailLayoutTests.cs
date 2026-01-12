using Blazor.Shared.Popup.Services;
using Blazor.Shared.Settings.Components;
using Blazor.Shared.Settings.Services;
using Bunit;
using AwesomeAssertions;
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
    public void AssertExistingElementOnDirtyState(string expectedCssClass)
    {
        // Arrange
        IControlPanelService? panelServiceMock = null;

        using var ctx = new TestContext();
        ctx.SetupBlazorSharedSettings(setup =>
        {
            panelServiceMock = setup.ControlPanelService;

            setup.Services.AddScoped(_ => Substitute.For<IActiveControlPanelPageProvider>());
            setup.Services.AddScoped(_ => Substitute.For<IControlPanelPageRegistry>());
            setup.Services.AddScoped(_ => Substitute.For<IControlPanelRequest>());
            setup.Services.AddScoped(_ => Substitute.For<INavigateBackRequest>());

            setup.FakeAuthenticationStateProvider = true;
            setup.Services.AddScoped(_ => Substitute.For<IControlPanelRegistryItemCache>());

            setup.Services.AddScoped(_ => Substitute.For<ILoadingIndicationPlacementBehavior>());

        });

        var component = ctx.RenderComponent<SettingsContainer>();

        panelServiceMock?.IsDirty
            .Returns(true);

        // Act
        component.Render();

        // Assert
        component.FindAll(expectedCssClass).Should().HaveCount(1);
    }
}
