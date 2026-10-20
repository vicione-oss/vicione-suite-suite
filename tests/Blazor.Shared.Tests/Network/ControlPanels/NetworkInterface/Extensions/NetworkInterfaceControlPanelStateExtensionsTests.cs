using System.Net;
using System.Net.NetworkInformation;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Extensions;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Models;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using Core.Shared.HostManagement.Requests;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;
using Sdk.Client.Infrastructure;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Tests.Network.ControlPanels.NetworkInterface.Extensions;

public sealed class NetworkInterfaceControlPanelStateExtensionsTests
{
    private const string OriginalMac = "11:22:33:44:55:66";

    private readonly ISystemConfigurationService _systemConfigurationService = Substitute.For<ISystemConfigurationService>();

    private void SetupSystemConfiguration(UserDefinedMACAddressSettings userDefinedMACAddress)
    {
        _systemConfigurationService.SystemConfiguration.Returns(new SystemConfiguration
        {
            NetworkInterfaces =
            [
                new NetworkInterfaceDetail
                {
                    CommonInformation = new NetworkInterfaceCommonInformation { Name = "eth0", Enabled = true, UserDefinedMACAddress = userDefinedMACAddress },
                    IPv4 = new IPv4Settings()
                }
            ]
        });
    }

    [Fact]
    public void Should_return_user_defined_mac_address_when_enabled()
    {
        // Arrange
        SetupSystemConfiguration(new UserDefinedMACAddressSettings { Enabled = true, Address = PhysicalAddress.Parse("AA:BB:CC:DD:EE:FF") });
        var state = new NetworkInterfaceControlPanelState { NetworkInterfaceIndex = 0, OriginalPhysicalAddress = OriginalMac };

        // Act
        var macAddress = state.GetMacAddress(_systemConfigurationService);

        // Assert
        macAddress.Should().Be("AA:BB:CC:DD:EE:FF");
    }

    [Fact]
    public void Should_return_original_mac_address_when_user_defined_mac_address_is_disabled()
    {
        // Arrange
        SetupSystemConfiguration(new UserDefinedMACAddressSettings());
        var state = new NetworkInterfaceControlPanelState { NetworkInterfaceIndex = 0, OriginalPhysicalAddress = OriginalMac };

        // Act
        var macAddress = state.GetMacAddress(_systemConfigurationService);

        // Assert
        macAddress.Should().Be(OriginalMac);
    }

    [Fact]
    public void Should_return_unknown_when_user_defined_mac_address_is_disabled_and_original_mac_address_is_unknown()
    {
        // Arrange
        SetupSystemConfiguration(new UserDefinedMACAddressSettings());
        var state = new NetworkInterfaceControlPanelState { NetworkInterfaceIndex = 0 };

        // Act
        var macAddress = state.GetMacAddress(_systemConfigurationService);

        // Assert
        macAddress.Should().Be(CommonVocabulary.Unknown);
    }

    [Fact]
    public void Should_return_unknown_when_network_interface_is_not_found()
    {
        // Arrange
        SetupSystemConfiguration(new UserDefinedMACAddressSettings());
        var state = new NetworkInterfaceControlPanelState { NetworkInterfaceIndex = 5, OriginalPhysicalAddress = OriginalMac };

        // Act
        var macAddress = state.GetMacAddress(_systemConfigurationService);

        // Assert
        macAddress.Should().Be(CommonVocabulary.Unknown);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void Should_return_the_static_dns_name_servers_only_while_they_are_enabled(bool enabled, int expectedCount)
    {
        // Arrange
        _systemConfigurationService.SystemConfiguration.Returns(new SystemConfiguration
        {
            NetworkDNSSettings = new NetworkDNSSettings
            {
                NameServers = new() { Enabled = enabled, Addresses = [IPAddress.Parse("8.8.8.8")] }
            }
        });
        var state = new NetworkInterfaceControlPanelState();

        // Act
        var nameServers = state.GetStaticDnsNameServers(_systemConfigurationService);

        // Assert
        nameServers.Should().HaveCount(expectedCount);
    }

    [Fact]
    public void Should_return_the_configured_addresses_of_a_static_configuration()
    {
        // Arrange
        SetupIPv4Settings(dhcpEnabled: false);
        var state = new NetworkInterfaceControlPanelState();

        // Act
        var ipAddresses = state.GetIpAddresses(_systemConfigurationService);

        // Assert
        ipAddresses.Should().ContainSingle().Which.Should().Be(new IpAddressInformation(IpConfigurationMode.Manual,
            IPAddress.Parse("192.168.1.10"), IPAddress.Parse("255.255.255.0"), IPAddress.Parse("192.168.1.1")));
    }

    [Fact]
    public void Should_return_the_leased_address_when_dhcp_is_enabled()
    {
        // Arrange
        SetupIPv4Settings(dhcpEnabled: true);
        var state = new NetworkInterfaceControlPanelState { DHCPLease = CreateLease("10.0.0.5") };

        // Act
        var ipAddresses = state.GetIpAddresses(_systemConfigurationService);

        // Assert
        ipAddresses.Should().ContainSingle().Which.Address.Should().Be(IPAddress.Parse("10.0.0.5"));
    }

    [Fact]
    public void Should_return_a_dhcp_entry_without_address_when_no_lease_is_known()
    {
        // Arrange
        SetupIPv4Settings(dhcpEnabled: true);
        var state = new NetworkInterfaceControlPanelState();

        // Act
        var ipAddresses = state.GetIpAddresses(_systemConfigurationService);

        // Assert
        ipAddresses.Should().ContainSingle().Which.Should().Be(new IpAddressInformation(IpConfigurationMode.AutomaticDhcp,
            IPAddress.None, IPAddress.None, null));
    }

    [Fact]
    public async Task Should_fetch_the_lease_when_dhcp_is_enabled_and_no_leased_address_is_known()
    {
        // Arrange
        SetupIPv4Settings(dhcpEnabled: true);
        var mediator = Substitute.For<IUiMediator>();
        mediator.Request<GetDHCPLeaseInformation, GetDHCPLeaseInformationResponse>(Arg.Any<GetDHCPLeaseInformation>(), Arg.Any<CancellationToken>())
            .Returns(new GetDHCPLeaseInformationResponse { DHCPLease = CreateLease("10.0.0.5") });
        var state = new NetworkInterfaceControlPanelState { DHCPLease = DHCPLease.Empty };

        // Act
        await state.EnsureDHCPLease(mediator, _systemConfigurationService, TestContext.Current.CancellationToken);

        // Assert
        state.DHCPLease!.IPv4Detail!.IPAddress.Should().Be(IPAddress.Parse("10.0.0.5"));
        await mediator.Received(1).Request<GetDHCPLeaseInformation, GetDHCPLeaseInformationResponse>(
            new GetDHCPLeaseInformation("eth0"), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public async Task Should_not_fetch_the_lease_when_it_is_not_needed(bool dhcpEnabled, bool leaseKnown, bool resetRunning)
    {
        // Arrange
        SetupIPv4Settings(dhcpEnabled);
        var mediator = Substitute.For<IUiMediator>();
        var state = new NetworkInterfaceControlPanelState { DHCPLease = leaseKnown ? CreateLease("10.0.0.5") : null };

        if (resetRunning)
            state.BeginLoading();

        // Act
        await state.EnsureDHCPLease(mediator, _systemConfigurationService, TestContext.Current.CancellationToken);

        // Assert
        await mediator.DidNotReceive().Request<GetDHCPLeaseInformation, GetDHCPLeaseInformationResponse>(
            Arg.Any<GetDHCPLeaseInformation>(), Arg.Any<CancellationToken>());
    }

    private void SetupIPv4Settings(bool dhcpEnabled)
        => _systemConfigurationService.SystemConfiguration.Returns(new SystemConfiguration
        {
            NetworkInterfaces =
            [
                new NetworkInterfaceDetail
                {
                    CommonInformation = new NetworkInterfaceCommonInformation { Name = "eth0", Enabled = true },
                    IPv4 = new IPv4Settings
                    {
                        DHCPEnabled = dhcpEnabled,
                        IPv4Details = [new IPv4Detail { IPAddress = IPAddress.Parse("192.168.1.10"), Netmask = IPAddress.Parse("255.255.255.0") }],
                        Gateway = IPAddress.Parse("192.168.1.1")
                    }
                }
            ]
        });

    private static DHCPLease CreateLease(string ipAddress)
        => new() { IPv4Detail = new IPv4Detail { IPAddress = IPAddress.Parse(ipAddress), Netmask = IPAddress.Parse("255.255.255.0") } };
}
