using System.Net;
using Blazor.Shared.Onboarding.Extensions;
using Blazor.Shared.Onboarding.Models;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;

namespace Blazor.Shared.Tests.Onboarding.Extensions;

public class INetworkInterfaceConfigurationExtensionsTests
{
    private const int LocalNetworkNumber = 2;

    private readonly SystemConfiguration _systemConfiguration = SystemConfigurations.CreateDhcp();

    private NetworkInterfaceDetail LocalNetworkInterface
        => _systemConfiguration.NetworkInterfaces.Single(i => i.CommonInformation.Name == "lan2");

    private void ConfigureManualLocalNetwork(string? gateway)
    {
        var ipV4 = LocalNetworkInterface.IPv4;

        ipV4.DHCPEnabled = false;
        ipV4.IPv4Details.Add(new IPv4Detail { IPAddress = IPAddress.Parse("192.168.1.10"), Netmask = IPAddress.Parse("255.255.255.0") });
        ipV4.Gateway = gateway is null ? null : IPAddress.Parse(gateway);
    }

    public sealed class ApplyTo : INetworkInterfaceConfigurationExtensionsTests
    {
        [Fact]
        public void Should_map_dhcp_interface()
        {
            // Arrange
            var target = new NetworkInterfaceConfiguration(LocalNetworkNumber) { ConfigurationMode = IpConfigurationMode.Manual, IpAddress = "1.2.3.4" };

            // Act
            target.ApplyTo(_systemConfiguration);

            // Assert
            target.ConfigurationMode.Should().Be(IpConfigurationMode.AutomaticDhcp);
            target.IpAddress.Should().BeEmpty();
            target.SubnetMask.Should().BeEmpty();
            target.DefaultGateway.Should().BeNull();
            target.DnsServer.Should().BeNull();
        }

        [Fact]
        public void Should_map_manual_interface()
        {
            // Arrange
            ConfigureManualLocalNetwork(gateway: "192.168.1.1");
            var target = new NetworkInterfaceConfiguration(LocalNetworkNumber);

            // Act
            target.ApplyTo(_systemConfiguration);

            // Assert
            target.ConfigurationMode.Should().Be(IpConfigurationMode.Manual);
            target.IpAddress.Should().Be("192.168.1.10");
            target.SubnetMask.Should().Be("255.255.255.0");
            target.DefaultGateway.Should().Be("192.168.1.1");
        }

        [Fact]
        public void Should_map_manual_interface_without_gateway()
        {
            // Arrange
            ConfigureManualLocalNetwork(gateway: null);
            var target = new NetworkInterfaceConfiguration(LocalNetworkNumber) { DefaultGateway = "192.168.1.1" };

            // Act
            target.ApplyTo(_systemConfiguration);

            // Assert
            target.DefaultGateway.Should().BeNull();
        }

        [Fact]
        public void Should_map_first_name_server_when_name_servers_are_enabled()
        {
            // Arrange
            var nameServers = _systemConfiguration.NetworkDNSSettings.NameServers;
            nameServers.Enabled = true;
            nameServers.Addresses.Add(IPAddress.Parse("192.168.1.2"));
            nameServers.Addresses.Add(IPAddress.Parse("192.168.1.3"));

            var target = new NetworkInterfaceConfiguration(LocalNetworkNumber);

            // Act
            target.ApplyTo(_systemConfiguration);

            // Assert
            target.DnsServer.Should().Be("192.168.1.2");
        }

        [Fact]
        public void Should_clear_dns_server_when_name_servers_are_disabled()
        {
            // Arrange
            var nameServers = _systemConfiguration.NetworkDNSSettings.NameServers;
            nameServers.Enabled = false;
            nameServers.Addresses.Add(IPAddress.Parse("192.168.1.2"));

            var target = new NetworkInterfaceConfiguration(LocalNetworkNumber) { DnsServer = "192.168.1.2" };

            // Act
            target.ApplyTo(_systemConfiguration);

            // Assert
            target.DnsServer.Should().BeNull();
        }

        [Fact]
        public void Should_leave_target_unchanged_when_interface_is_missing()
        {
            // Arrange
            var target = new NetworkInterfaceConfiguration(9)
            {
                ConfigurationMode = IpConfigurationMode.Manual,
                IpAddress = "10.0.0.5",
                SubnetMask = "255.0.0.0",
                DefaultGateway = "10.0.0.1",
                DnsServer = "10.0.0.2"
            };

            // Act
            target.ApplyTo(_systemConfiguration);

            // Assert
            target.Should().BeEquivalentTo(new NetworkInterfaceConfiguration(9)
            {
                ConfigurationMode = IpConfigurationMode.Manual,
                IpAddress = "10.0.0.5",
                SubnetMask = "255.0.0.0",
                DefaultGateway = "10.0.0.1",
                DnsServer = "10.0.0.2"
            });
        }

        [Fact]
        public void Should_copy_all_values_from_another_configuration()
        {
            // Arrange
            var source = new NetworkInterfaceConfiguration(LocalNetworkNumber)
            {
                ConfigurationMode = IpConfigurationMode.Manual,
                IpAddress = "192.168.1.10",
                SubnetMask = "255.255.255.0",
                DefaultGateway = "192.168.1.1",
                DnsServer = "192.168.1.2"
            };
            var target = new NetworkInterfaceConfiguration(LocalNetworkNumber);

            // Act
            target.ApplyTo(source);

            // Assert
            target.Should().BeEquivalentTo(source);
        }
    }
}
