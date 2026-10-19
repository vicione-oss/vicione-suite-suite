using Blazor.Shared.Network.ControlPanels.NetworkInterface.Components;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Extensions;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Tests.Network.ControlPanels.NetworkInterface.Extensions;

public class IServiceCollectionExtensionsTests
{
    public sealed class AddNetworkInterfaceControlPanel
    {
        [Theory]
        [InlineData(typeof(NetworkInterfaceGeneralControlPanel))]
        [InlineData(typeof(NetworkInterfaceTcpIpControlPanel))]
        [InlineData(typeof(NetworkInterfaceHardwareControlPanel))]
        public void Should_configure_save_and_reset_handler_for_each_tab(Type controlPanelType)
        {
            // Arrange
            var services = new ServiceCollection();
            var handlerServiceKey = typeof(ControlPanelServiceKey<>).MakeGenericType(controlPanelType);

            // Act
            services.AddNetworkInterfaceControlPanel();

            // Assert - a tab without handlers of its own would neither load nor save
            services.Should().Contain(d => d.IsKeyedService && (Type?)d.ServiceKey == handlerServiceKey
                && d.ServiceType == typeof(IControlPanelSaveHandler<NetworkInterfaceControlPanelState>));

            services.Should().Contain(d => d.IsKeyedService && (Type?)d.ServiceKey == handlerServiceKey
                && d.ServiceType == typeof(IControlPanelResetHandler<NetworkInterfaceControlPanelState>));
        }

        [Fact]
        public void Should_register_one_saver_per_scope_for_the_save_handlers_of_all_tabs()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddNetworkInterfaceControlPanel();

            // Assert
            services.Should().ContainSingle(d => d.ServiceType == typeof(NetworkInterfaceSaver))
                .Which.Lifetime.Should().Be(ServiceLifetime.Scoped);
        }
    }
}
