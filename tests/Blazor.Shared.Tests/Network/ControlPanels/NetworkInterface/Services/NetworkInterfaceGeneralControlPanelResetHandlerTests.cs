using Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using Core.Shared.HostManagement.Requests;
using HostManagement.Shared.Contracts.Network;

namespace Blazor.Shared.Tests.Network.ControlPanels.NetworkInterface.Services;

public sealed class NetworkInterfaceGeneralControlPanelResetHandlerTests : NetworkInterfaceControlPanelResetHandlerTestsBase
{
    private protected override NetworkInterfaceControlPanelResetHandlerBase CreateResetHandler()
        => new NetworkInterfaceGeneralControlPanelResetHandler(Mediator, SystemConfigurationService);

    [Fact]
    public async Task Should_reset_to_defaults_when_network_interface_not_found()
    {
        // Arrange
        SetupSystemConfiguration();
        var state = new NetworkInterfaceControlPanelState { NetworkInterfaceIndex = 5, Name = "eth0", Enabled = true, DHCPLease = DHCPLease.Empty };

        // Act
        await CreateResetHandler().Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.Name.Should().BeEmpty();
        state.Enabled.Should().BeFalse();
        state.DHCPLease.Should().BeNull();
        state.IpAddresses.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_load_the_enabled_state_from_system_configuration()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());
        var state = new NetworkInterfaceControlPanelState();

        // Act
        await CreateResetHandler().Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task Should_load_the_ip_addresses_of_a_static_configuration()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface(new IPv4Settings
        {
            IPv4Details = [new IPv4Detail { IPAddress = System.Net.IPAddress.Parse("192.168.1.10"), Netmask = System.Net.IPAddress.Parse("255.255.255.0") }],
            Gateway = System.Net.IPAddress.Parse("192.168.1.1")
        }));
        var state = new NetworkInterfaceControlPanelState();

        // Act
        await CreateResetHandler().Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.IpAddresses.Should().ContainSingle()
            .Which.Configuration.Should().Be(IpConfigurationMode.Manual);
    }

    [Fact]
    public async Task Should_load_the_dhcp_lease_and_the_original_mac_address()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface(new IPv4Settings { DHCPEnabled = true }));

        Mediator.Request<GetDHCPLeaseInformation, GetDHCPLeaseInformationResponse>(
                Arg.Any<GetDHCPLeaseInformation>(), Arg.Any<CancellationToken>())
            .Returns(new GetDHCPLeaseInformationResponse { DHCPLease = DHCPLease.Empty });

        Mediator.Request<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>(
                Arg.Any<GetOriginalPhysicalAddress>(), Arg.Any<CancellationToken>())
            .Returns(new GetOriginalPhysicalAddressResponse { OriginalPhysicalAddress = "11:22:33:44:55:66" });

        var state = new NetworkInterfaceControlPanelState();

        // Act
        await CreateResetHandler().Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.DHCPLease.Should().NotBeNull();
        state.OriginalPhysicalAddress.Should().Be("11:22:33:44:55:66");
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
        state.Enabled.Should().BeTrue();
        state.GeneralHasChanges.Should().BeFalse();

        state.IpV4ConfigurationMode.Should().Be(IpConfigurationMode.Manual);
        state.FirstIpV4Detail.IpAddress.Should().Be("192.168.99.10");
        state.VLanId.Should().Be("42");
        state.MacAddress.Should().Be("AA:BB:CC:DD:EE:FF");
        state.TcpIpHasChanges.Should().BeTrue();
        state.HardwareHasChanges.Should().BeTrue();
    }
}
