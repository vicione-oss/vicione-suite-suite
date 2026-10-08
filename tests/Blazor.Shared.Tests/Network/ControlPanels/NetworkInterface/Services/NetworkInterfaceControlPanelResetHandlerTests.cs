using System.Net.NetworkInformation;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using Core.Shared.HostManagement.Requests;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Tests.Network.ControlPanels.NetworkInterface.Services;

public sealed class NetworkInterfaceControlPanelResetHandlerTests
{
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly ISystemConfigurationService _systemConfigurationService = Substitute.For<ISystemConfigurationService>();

    public NetworkInterfaceControlPanelResetHandlerTests()
    {
        _mediator.Request<GetDHCPLeaseInformation, GetDHCPLeaseInformationResponse>(
                Arg.Any<GetDHCPLeaseInformation>(), Arg.Any<CancellationToken>())
            .Returns(new GetDHCPLeaseInformationResponse());

        _mediator.Request<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>(
                Arg.Any<GetOriginalPhysicalAddress>(), Arg.Any<CancellationToken>())
            .Returns(new GetOriginalPhysicalAddressResponse());
    }

    private ServiceProvider SetupServiceProvider()
    {
        var services = new ServiceCollection()
            .AddScoped(_ => _mediator)
            .AddScoped(_ => _systemConfigurationService)
            .AddScoped<IControlPanelResetHandler<NetworkInterfaceControlPanelState>, NetworkInterfaceControlPanelResetHandler>();

        return services.BuildServiceProvider();
    }

    private void SetupSystemConfiguration(params NetworkInterfaceDetail[] networkInterfaces)
    {
        _systemConfigurationService.SystemConfiguration.Returns(new SystemConfiguration
        {
            NetworkInterfaces = [.. networkInterfaces]
        });
    }

    [Fact]
    public async Task Should_end_loading_after_reset()
    {
        // Arrange
        SetupSystemConfiguration();
        await using var serviceProvider = SetupServiceProvider();

        var state = new NetworkInterfaceControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<NetworkInterfaceControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task Should_reset_to_defaults_when_network_interface_not_found()
    {
        // Arrange
        SetupSystemConfiguration();
        await using var serviceProvider = SetupServiceProvider();

        var state = new NetworkInterfaceControlPanelState { NetworkInterfaceIndex = 5 };
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<NetworkInterfaceControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.Name.Should().BeEmpty();
        state.Enabled.Should().BeFalse();
        state.IpV4ConfigurationMode.Should().Be(IpConfigurationMode.AutomaticDhcp);
        state.DefaultGateway.Should().BeNull();
        state.MacAddressManually.Should().BeFalse();
        state.MacAddress.Should().BeEmpty();
        state.VLanEnabled.Should().BeFalse();
        state.VLanId.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_load_interface_name_and_enabled_state_from_system_configuration()
    {
        // Arrange
        var networkInterface = new NetworkInterfaceDetail
        {
            CommonInformation = new NetworkInterfaceCommonInformation { Name = "eth0", Enabled = true },
            IPv4 = new IPv4Settings()
        };
        SetupSystemConfiguration(networkInterface);
        await using var serviceProvider = SetupServiceProvider();

        var state = new NetworkInterfaceControlPanelState { NetworkInterfaceIndex = 0 };
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<NetworkInterfaceControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.Name.Should().Be("eth0");
        state.Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task Should_set_ip_configuration_mode_to_automatic_dhcp_when_dhcp_enabled_and_lease_available()
    {
        // Arrange
        var networkInterface = new NetworkInterfaceDetail
        {
            CommonInformation = new NetworkInterfaceCommonInformation { Name = "eth0", Enabled = true },
            IPv4 = new IPv4Settings { DHCPEnabled = true }
        };
        SetupSystemConfiguration(networkInterface);

        _mediator.Request<GetDHCPLeaseInformation, GetDHCPLeaseInformationResponse>(
                Arg.Any<GetDHCPLeaseInformation>(), Arg.Any<CancellationToken>())
            .Returns(new GetDHCPLeaseInformationResponse { DHCPLease = DHCPLease.Empty });

        await using var serviceProvider = SetupServiceProvider();

        var state = new NetworkInterfaceControlPanelState { NetworkInterfaceIndex = 0 };
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<NetworkInterfaceControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.IpV4ConfigurationMode.Should().Be(IpConfigurationMode.AutomaticDhcp);
        state.DHCPLease.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_set_ip_configuration_mode_to_manual_when_dhcp_disabled()
    {
        // Arrange
        var networkInterface = new NetworkInterfaceDetail
        {
            CommonInformation = new NetworkInterfaceCommonInformation { Name = "eth0", Enabled = true },
            IPv4 = new IPv4Settings { DHCPEnabled = false }
        };
        SetupSystemConfiguration(networkInterface);

        await using var serviceProvider = SetupServiceProvider();

        var state = new NetworkInterfaceControlPanelState { NetworkInterfaceIndex = 0, DHCPLease = DHCPLease.Empty };
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<NetworkInterfaceControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.IpV4ConfigurationMode.Should().Be(IpConfigurationMode.Manual);
        state.DHCPLease.Should().BeNull();
    }

    [Fact]
    public async Task Should_set_mac_address_manually_when_user_defined_mac_address_is_enabled()
    {
        // Arrange
        const string customMac = "AA:BB:CC:DD:EE:FF";
        var networkInterface = new NetworkInterfaceDetail
        {
            CommonInformation = new NetworkInterfaceCommonInformation { Name = "eth0", Enabled = true, UserDefinedMACAddress = new() { Enabled = true, Address = PhysicalAddress.Parse(customMac) } },
            IPv4 = new IPv4Settings()
        };
        SetupSystemConfiguration(networkInterface);

        await using var serviceProvider = SetupServiceProvider();

        var state = new NetworkInterfaceControlPanelState { NetworkInterfaceIndex = 0 };
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<NetworkInterfaceControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.MacAddressManually.Should().BeTrue();
        state.MacAddress.Should().Be(customMac);
    }

    [Fact]
    public async Task Should_not_set_mac_address_manually_when_user_defined_mac_address_is_disabled()
    {
        // Arrange
        var networkInterface = new NetworkInterfaceDetail
        {
            CommonInformation = new NetworkInterfaceCommonInformation { Name = "eth0", Enabled = true, UserDefinedMACAddress = new() { Enabled = false, Address = PhysicalAddress.Parse("AA:BB:CC:DD:EE:FF") } },
            IPv4 = new IPv4Settings()
        };
        SetupSystemConfiguration(networkInterface);

        _mediator.Request<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>(
                Arg.Any<GetOriginalPhysicalAddress>(), Arg.Any<CancellationToken>())
            .Returns(new GetOriginalPhysicalAddressResponse { OriginalPhysicalAddress = "11:22:33:44:55:66" });

        await using var serviceProvider = SetupServiceProvider();

        var state = new NetworkInterfaceControlPanelState { NetworkInterfaceIndex = 0 };
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<NetworkInterfaceControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.MacAddressManually.Should().BeFalse();
        state.MacAddress.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_load_original_mac_address_of_network_interface()
    {
        // Arrange
        const string originalMac = "11:22:33:44:55:66";
        var networkInterface = new NetworkInterfaceDetail
        {
            CommonInformation = new NetworkInterfaceCommonInformation { Name = "eth0", Enabled = true },
            IPv4 = new IPv4Settings()
        };
        SetupSystemConfiguration(networkInterface);

        _mediator.Request<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>(
                Arg.Any<GetOriginalPhysicalAddress>(), Arg.Any<CancellationToken>())
            .Returns(new GetOriginalPhysicalAddressResponse { OriginalPhysicalAddress = originalMac });

        await using var serviceProvider = SetupServiceProvider();

        var state = new NetworkInterfaceControlPanelState { NetworkInterfaceIndex = 0 };
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<NetworkInterfaceControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.OriginalMacAddress.Should().Be(originalMac);
    }

    [Fact]
    public async Task Should_set_vlan_settings_from_network_interface()
    {
        // Arrange
        var networkInterface = new NetworkInterfaceDetail
        {
            CommonInformation = new NetworkInterfaceCommonInformation { Name = "eth0", Enabled = true },
            IPv4 = new IPv4Settings(),
            VLAN = new VLANSettings { Enabled = true, ID = 100 }
        };
        SetupSystemConfiguration(networkInterface);

        await using var serviceProvider = SetupServiceProvider();

        var state = new NetworkInterfaceControlPanelState { NetworkInterfaceIndex = 0 };
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<NetworkInterfaceControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.VLanEnabled.Should().BeTrue();
        state.VLanId.Should().Be("100");
    }

    [Fact]
    public async Task Should_load_ipv4_details_from_network_interface()
    {
        // Arrange
        var networkInterface = new NetworkInterfaceDetail
        {
            CommonInformation = new NetworkInterfaceCommonInformation { Name = "eth0", Enabled = true },
            IPv4 = new IPv4Settings
            {
                IPv4Details = [
                    new IPv4Detail { IPAddress = System.Net.IPAddress.Parse("192.168.1.10"), Netmask = System.Net.IPAddress.Parse("255.255.255.0") },
                    new IPv4Detail { IPAddress = System.Net.IPAddress.Parse("10.0.0.1"), Netmask = System.Net.IPAddress.Parse("255.0.0.0") }
                ]
            }
        };
        SetupSystemConfiguration(networkInterface);

        await using var serviceProvider = SetupServiceProvider();

        var state = new NetworkInterfaceControlPanelState { NetworkInterfaceIndex = 0 };
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<NetworkInterfaceControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.FirstIpV4Detail.IpAddress.Should().Be("192.168.1.10");
        state.FirstIpV4Detail.SubnetMask.Should().Be("255.255.255.0");
        state.AdditionalIpV4Details.Should().HaveCount(1);
        state.AdditionalIpV4Details[0].IpAddress.Should().Be("10.0.0.1");
    }
}
