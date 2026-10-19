using Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using Core.Shared.HostManagement.Requests;
using HostManagement.Shared.Contracts.Network;

namespace Blazor.Shared.Tests.Network.ControlPanels.NetworkInterface.Services;

public sealed class NetworkInterfaceTcpIpControlPanelResetHandlerTests : NetworkInterfaceControlPanelResetHandlerTestsBase
{
    private protected override NetworkInterfaceControlPanelResetHandlerBase CreateResetHandler()
        => new NetworkInterfaceTcpIpControlPanelResetHandler(Mediator, SystemConfigurationService);

    [Fact]
    public async Task Should_reset_to_defaults_when_network_interface_not_found()
    {
        // Arrange
        SetupSystemConfiguration();
        var state = CreateEditedState();
        state.NetworkInterfaceIndex = 5;

        // Act
        await CreateResetHandler().Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.IpV4ConfigurationMode.Should().Be(IpConfigurationMode.AutomaticDhcp);
        state.DefaultGateway.Should().BeNull();
        state.VLanEnabled.Should().BeFalse();
        state.VLanId.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_set_ip_configuration_mode_to_automatic_dhcp_when_dhcp_enabled_and_lease_available()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface(new IPv4Settings { DHCPEnabled = true }));

        Mediator.Request<GetDHCPLeaseInformation, GetDHCPLeaseInformationResponse>(
                Arg.Any<GetDHCPLeaseInformation>(), Arg.Any<CancellationToken>())
            .Returns(new GetDHCPLeaseInformationResponse { DHCPLease = DHCPLease.Empty });

        var state = new NetworkInterfaceControlPanelState();

        // Act
        await CreateResetHandler().Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.IpV4ConfigurationMode.Should().Be(IpConfigurationMode.AutomaticDhcp);
        state.DHCPLease.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_set_ip_configuration_mode_to_manual_when_dhcp_disabled()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface(new IPv4Settings { DHCPEnabled = false }));
        var state = new NetworkInterfaceControlPanelState { DHCPLease = DHCPLease.Empty };

        // Act
        await CreateResetHandler().Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.IpV4ConfigurationMode.Should().Be(IpConfigurationMode.Manual);
        state.DHCPLease.Should().BeNull();
    }

    [Fact]
    public async Task Should_set_vlan_settings_from_network_interface()
    {
        // Arrange
        var networkInterface = CreateNetworkInterface();
        networkInterface.VLAN = new VLANSettings { Enabled = true, ID = 100 };
        SetupSystemConfiguration(networkInterface);
        var state = new NetworkInterfaceControlPanelState();

        // Act
        await CreateResetHandler().Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.VLanEnabled.Should().BeTrue();
        state.VLanId.Should().Be("100");
    }

    [Fact]
    public async Task Should_load_ipv4_details_from_network_interface()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface(new IPv4Settings
        {
            IPv4Details = [
                new IPv4Detail { IPAddress = System.Net.IPAddress.Parse("192.168.1.10"), Netmask = System.Net.IPAddress.Parse("255.255.255.0") },
                new IPv4Detail { IPAddress = System.Net.IPAddress.Parse("10.0.0.1"), Netmask = System.Net.IPAddress.Parse("255.0.0.0") }
            ]
        }));
        var state = new NetworkInterfaceControlPanelState();

        // Act
        await CreateResetHandler().Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.FirstIpV4Detail.IpAddress.Should().Be("192.168.1.10");
        state.FirstIpV4Detail.SubnetMask.Should().Be("255.255.255.0");
        state.AdditionalIpV4Details.Should().HaveCount(1);
        state.AdditionalIpV4Details[0].IpAddress.Should().Be("10.0.0.1");
    }

    [Fact]
    public async Task Should_leave_the_edits_of_the_other_tabs_untouched()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());
        var state = CreateEditedState();

        // Act
        await CreateResetHandler().Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.FirstIpV4Detail.IpAddress.Should().NotBe("192.168.99.10");
        state.VLanEnabled.Should().BeFalse();
        state.TcpIpHasChanges.Should().BeFalse();

        state.Enabled.Should().BeFalse();
        state.MacAddress.Should().Be("AA:BB:CC:DD:EE:FF");
        state.GeneralHasChanges.Should().BeTrue();
        state.HardwareHasChanges.Should().BeTrue();
    }
}
