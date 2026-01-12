using Blazor.Shared.Network.ControlPanels.NetworkInterface.Components;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;
using Blazor.Shared.Popup.Services;
using Blazor.Shared.Settings.Components;
using Blazor.Shared.Settings.Services;
using Bunit;
using Core.Shared.HostManagement.Services;
using HostManagement.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Services;
using Xunit;

namespace Blazor.Shared.Tests.Network.ControlPanels;

public class NetworkInterfaceControlPanelTests
{
    [Fact]
    public void ComponentShouldRender()
    {
        // Arrange
        var timeProvider = Substitute.For<TimeProvider>();
        var systemConfigurationService = Substitute.For<ISystemConfigurationService>();
        timeProvider.LocalTimeZone.Returns(TimeZoneInfo.Utc);

        systemConfigurationService.SystemConfiguration.Returns(new SystemConfiguration());

        using var ctx = new TestContext();
        ctx.SetupBlazorSharedSettings(setup =>
        {
            setup.Services.AddSingleton(Substitute.For<IControlPanelRequest>());
            setup.Services.AddSingleton(Substitute.For<INavigateBackRequest>());
            setup.Services.AddSingleton(Substitute.For<ILoadingIndicationPlacementBehavior>());
            setup.Services.AddSingleton(systemConfigurationService);
            setup.Services.AddKeyedScoped(Sdk.Constants.ClientTimeProviderServiceKey, (_, __) => timeProvider);

            setup.Services.SetupControlPanelRegistry(registryItem =>
            {
                var controlPanelState = new NetworkInterfaceControlPanelState();

                registryItem.ComponentType.Returns(typeof(NetworkInterfaceControlPanel));
                registryItem.Descriptor.Returns(new NetworkInterfaceControlPanelDescriptor(controlPanelState, systemConfigurationService));
                registryItem.State.Returns(controlPanelState);
            });

            setup.Services.SetupControlPanelRegistryCache();
        });

        // Act + Assert
        var component = ctx.RenderComponent<SettingsContainer>();
        Assert.NotNull(component);

        var controlPanel = component.FindComponent<NetworkInterfaceControlPanel>();
        Assert.NotNull(controlPanel);
    }
}
