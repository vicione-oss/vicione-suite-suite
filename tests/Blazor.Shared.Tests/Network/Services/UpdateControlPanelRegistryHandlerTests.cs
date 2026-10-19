using Blazor.Shared.Network.ControlPanels.Dns.Components;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Components;
using Blazor.Shared.Network.ControlPanels.Ntp.Components;
using Blazor.Shared.Network.ControlPanels.Proxies.Components;
using Blazor.Shared.Network.ControlPanels.RemoteAccess.Components;
using Blazor.Shared.Network.Services;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Services;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Tests.Network.Services;

public sealed class UpdateControlPanelRegistryHandlerTests
{
    [Fact]
    public async Task Should_nest_the_general_tcp_ip_and_hardware_control_panels_in_tab_order_under_their_network_interface()
    {
        // Arrange
        var registry = new ControlPanelRegistry<SharedClientModule>(Substitute.For<IDefaultControlPanelGroupDescriptor>());

        var systemConfigurationService = Substitute.For<ISystemConfigurationService>();
        systemConfigurationService.SystemConfiguration.Returns(new SystemConfiguration
        {
            NetworkInterfaces =
            [
                new NetworkInterfaceDetail
                {
                    CommonInformation = new NetworkInterfaceCommonInformation { Name = "lan1", Enabled = true },
                    IPv4 = new IPv4Settings()
                }
            ]
        });

        var handler = new UpdateControlPanelRegistryHandler(registry, systemConfigurationService);

        // Act
        await handler.Execute(TestContext.Current.CancellationToken);

        // Assert
        var parent = registry.Single(i => i.ComponentType == typeof(NetworkInterfaceControlPanel));
        parent.AsParent.Should().NotBeNull();
        parent.Descriptor.HasContent.Should().BeFalse();

        registry.Where(i => i.ChildOf is not null && i.ChildOf == parent.AsParent)
            .OrderBy(i => i.Descriptor.Position)
            .Select(i => i.ComponentType)
            .Should().Equal(typeof(NetworkInterfaceGeneralControlPanel), typeof(NetworkInterfaceTcpIpControlPanel),
                typeof(NetworkInterfaceHardwareControlPanel));
    }

    [Fact]
    public async Task Should_list_the_other_network_control_panels_after_the_maximum_count_of_network_interfaces()
    {
        // Arrange
        var registry = new ControlPanelRegistry<SharedClientModule>(Substitute.For<IDefaultControlPanelGroupDescriptor>());

        var systemConfigurationService = Substitute.For<ISystemConfigurationService>();
        systemConfigurationService.SystemConfiguration.Returns(new SystemConfiguration
        {
            NetworkInterfaces = Enumerable.Range(1, ControlPanelNetworkCategoryDescriptor.MaxNetworkInterfaceCount)
                .Select(number => new NetworkInterfaceDetail
                {
                    CommonInformation = new NetworkInterfaceCommonInformation { Name = $"lan{number}", Enabled = true },
                    IPv4 = new IPv4Settings()
                })
                .ToList()
        });

        var handler = new UpdateControlPanelRegistryHandler(registry, systemConfigurationService);

        // Act
        await handler.Execute(TestContext.Current.CancellationToken);

        // Assert
        var settingsEntries = new SettingsModuleService(Substitute.For<ILogger<SettingsModuleService>>())
            .GetSettingsEntries(registry, groupPosition: 0, CommonVocabulary.Network)
            .Sort()
            .Select(e => e.ControlPanelRegistryItem.ComponentType)
            .ToList();

        settingsEntries.Should().Equal(
            Enumerable.Repeat(typeof(NetworkInterfaceControlPanel), ControlPanelNetworkCategoryDescriptor.MaxNetworkInterfaceCount)
                .Concat([typeof(DnsControlPanel), typeof(NtpControlPanel), typeof(ProxiesControlPanel), typeof(RemoteAccessControlPanel)]));
    }
}
