using System.Net;
using AwesomeAssertions;
using Blazor.Shared.Onboarding.Extensions;
using Blazor.Shared.Onboarding.Models;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;
using HostManagement.Shared.Contracts.Service;
using HostManagement.Shared.Enums;
using Xunit;

namespace Blazor.Shared.Tests.Onboarding.Services;

public sealed class SummaryWizardPageResetHandlerTests
{
    [Fact]
    public void SystemConfiguration_Equals_should_return_true_for_identical_instances()
    {
        // Arrange
        var config = new SystemConfiguration
        {
            NetworkInterfacesSettings = new NetworkInterfacesSettings(),
            NetworkDNSSettings = new NetworkDNSSettings { Hostname = "myhost" },
            NetworkProxySettings = new NetworkProxySettings(),
            NetworkNTPSettings = new NetworkNTPSettings()
        };

        // Act & Assert
        config.Equals(config).Should().BeTrue();
    }

    [Fact]
    public void SystemConfiguration_Equals_should_return_true_for_equivalent_instances()
    {
        // Arrange
        var config1 = new SystemConfiguration
        {
            NetworkInterfacesSettings = new NetworkInterfacesSettings(),
            NetworkDNSSettings = new NetworkDNSSettings { Hostname = "myhost" },
            NetworkProxySettings = new NetworkProxySettings(),
            NetworkNTPSettings = new NetworkNTPSettings()
        };

        var config2 = new SystemConfiguration
        {
            NetworkInterfacesSettings = new NetworkInterfacesSettings(),
            NetworkDNSSettings = new NetworkDNSSettings { Hostname = "myhost" },
            NetworkProxySettings = new NetworkProxySettings(),
            NetworkNTPSettings = new NetworkNTPSettings()
        };

        // Act & Assert
        config1.Equals(config2).Should().BeTrue();
    }

    [Fact]
    public void SystemConfiguration_Clone_should_equal_original()
    {
        // Arrange
        var original = new SystemConfiguration
        {
            NetworkInterfacesSettings = new NetworkInterfacesSettings(
            [
                new NetworkInterfaceDetail
                {
                    CommonInformation = new NetworkInterfaceCommonInformation { Name = "lan1", Enabled = true },
                    IPv4 = new IPv4Settings(
                    [
                        new IPv4Detail { IPAddress = IPAddress.Parse("10.0.0.1"), Netmask = IPAddress.Parse("255.255.255.0") }
                    ])
                    {
                        DHCPEnabled = false,
                        Gateway = IPAddress.Parse("10.0.0.254")
                    }
                },
                new NetworkInterfaceDetail
                {
                    CommonInformation = new NetworkInterfaceCommonInformation { Name = "lan2", Enabled = true },
                    IPv4 = new IPv4Settings { DHCPEnabled = true }
                }
            ]),
            NetworkDNSSettings = new NetworkDNSSettings
            {
                Hostname = "suite-host",
                NameServersEnabled = true,
                NameServers = { IPAddress.Parse("8.8.8.8") }
            },
            NetworkProxySettings = new NetworkProxySettings(),
            NetworkNTPSettings = new NetworkNTPSettings()
        };

        // Act
        var clone = original.Clone();

        // Assert
        clone.Should().NotBeSameAs(original);
        original.Equals(clone).Should().BeTrue("Clone should produce an equal SystemConfiguration");
    }

    [Fact]
    public void SystemConfiguration_Clone_then_UpdateFrom_with_same_dhcp_values_should_remain_equal()
    {
        // Arrange - system is in DHCP mode on lan2
        var original = new SystemConfiguration
        {
            NetworkInterfacesSettings = new NetworkInterfacesSettings(
            [
                new NetworkInterfaceDetail
                {
                    CommonInformation = new NetworkInterfaceCommonInformation { Name = "lan1", Enabled = true },
                    IPv4 = new IPv4Settings { DHCPEnabled = true }
                },
                new NetworkInterfaceDetail
                {
                    CommonInformation = new NetworkInterfaceCommonInformation { Name = "lan2", Enabled = true },
                    IPv4 = new IPv4Settings { DHCPEnabled = true }
                }
            ]),
            NetworkDNSSettings = new NetworkDNSSettings
            {
                Hostname = "suite-host",
                NameServersEnabled = false
            },
            NetworkProxySettings = new NetworkProxySettings(),
            NetworkNTPSettings = new NetworkNTPSettings()
        };

        // Target configuration matches current system (DHCP mode)
        // lan2 = local network, lan1 = internet connection
        var targetLocalNetwork = new NetworkInterfaceConfiguration(2)
        {
            ConfigurationMode = IpConfigurationMode.AutomaticDhcp
        };

        var targetInternetConnection = new NetworkInterfaceConfiguration(1)
        {
            ConfigurationMode = IpConfigurationMode.AutomaticDhcp
        };

        // Act
        var proposed = original.Clone();
        proposed.UpdateFrom(targetLocalNetwork);
        proposed.UpdateFrom(targetInternetConnection);
        proposed.NetworkDNSSettings.Hostname = "suite-host";

        // Assert
        original.Equals(proposed).Should().BeTrue(
            "When target configuration matches current system, proposed should equal current");
    }

    [Fact]
    public void SystemConfiguration_Clone_then_UpdateFrom_with_same_manual_values_should_remain_equal()
    {
        // Arrange - system is in manual mode on lan2 with specific IP settings
        var original = new SystemConfiguration
        {
            NetworkInterfacesSettings = new NetworkInterfacesSettings(
            [
                new NetworkInterfaceDetail
                {
                    CommonInformation = new NetworkInterfaceCommonInformation { Name = "lan1", Enabled = true },
                    IPv4 = new IPv4Settings { DHCPEnabled = true }
                },
                new NetworkInterfaceDetail
                {
                    CommonInformation = new NetworkInterfaceCommonInformation { Name = "lan2", Enabled = true },
                    IPv4 = new IPv4Settings(
                    [
                        new IPv4Detail { IPAddress = IPAddress.Parse("192.168.1.100"), Netmask = IPAddress.Parse("255.255.255.0") }
                    ])
                    {
                        DHCPEnabled = false,
                        Gateway = IPAddress.Parse("192.168.1.1")
                    }
                }
            ]),
            NetworkDNSSettings = new NetworkDNSSettings
            {
                Hostname = "suite-host",
                NameServersEnabled = true,
                NameServers = { IPAddress.Parse("8.8.8.8") }
            },
            NetworkProxySettings = new NetworkProxySettings(),
            NetworkNTPSettings = new NetworkNTPSettings()
        };

        // Target configuration matches current system (manual mode, same values)
        // lan2 = local network, lan1 = internet connection
        var targetLocalNetwork = new NetworkInterfaceConfiguration(2)
        {
            ConfigurationMode = IpConfigurationMode.Manual,
            IpAddress = "192.168.1.100",
            SubnetMask = "255.255.255.0",
            DefaultGateway = "192.168.1.1",
            DnsServer = "8.8.8.8"
        };

        var targetInternetConnection = new NetworkInterfaceConfiguration(1)
        {
            ConfigurationMode = IpConfigurationMode.AutomaticDhcp
        };

        // Act
        var proposed = original.Clone();
        proposed.UpdateFrom(targetLocalNetwork);
        proposed.UpdateFrom(targetInternetConnection);
        proposed.NetworkDNSSettings.Hostname = "suite-host";

        // Assert
        original.Equals(proposed).Should().BeTrue(
            "When target configuration matches current system, proposed should equal current");
    }

    [Fact]
    public void SystemConfiguration_Clone_then_UpdateFrom_with_different_hostname_should_not_be_equal()
    {
        // Arrange
        var original = new SystemConfiguration
        {
            NetworkInterfacesSettings = new NetworkInterfacesSettings(
            [
                new NetworkInterfaceDetail
                {
                    CommonInformation = new NetworkInterfaceCommonInformation { Name = "lan1", Enabled = true },
                    IPv4 = new IPv4Settings { DHCPEnabled = true }
                },
                new NetworkInterfaceDetail
                {
                    CommonInformation = new NetworkInterfaceCommonInformation { Name = "lan2", Enabled = true },
                    IPv4 = new IPv4Settings { DHCPEnabled = true }
                }
            ]),
            NetworkDNSSettings = new NetworkDNSSettings { Hostname = "old-host" },
            NetworkProxySettings = new NetworkProxySettings(),
            NetworkNTPSettings = new NetworkNTPSettings()
        };

        // Act
        var proposed = original.Clone();
        proposed.NetworkDNSSettings.Hostname = "new-host";

        // Assert
        original.Equals(proposed).Should().BeFalse(
            "Different hostname should make configurations unequal");
    }

    [Fact]
    public void SystemConfiguration_Clone_then_UpdateFrom_changing_dhcp_to_manual_should_not_be_equal()
    {
        // Arrange - system is in DHCP mode
        var original = new SystemConfiguration
        {
            NetworkInterfacesSettings = new NetworkInterfacesSettings(
            [
                new NetworkInterfaceDetail
                {
                    CommonInformation = new NetworkInterfaceCommonInformation { Name = "lan1", Enabled = true },
                    IPv4 = new IPv4Settings { DHCPEnabled = true }
                },
                new NetworkInterfaceDetail
                {
                    CommonInformation = new NetworkInterfaceCommonInformation { Name = "lan2", Enabled = true },
                    IPv4 = new IPv4Settings { DHCPEnabled = true }
                }
            ]),
            NetworkDNSSettings = new NetworkDNSSettings
            {
                Hostname = "suite-host",
                NameServersEnabled = false
            },
            NetworkProxySettings = new NetworkProxySettings(),
            NetworkNTPSettings = new NetworkNTPSettings()
        };

        // Target changes lan2 from DHCP to manual
        // lan2 = local network, lan1 = internet connection
        var targetLocalNetwork = new NetworkInterfaceConfiguration(2)
        {
            ConfigurationMode = IpConfigurationMode.Manual,
            IpAddress = "192.168.1.50",
            SubnetMask = "255.255.255.0",
            DefaultGateway = "192.168.1.1",
            DnsServer = "8.8.8.8"
        };

        var targetInternetConnection = new NetworkInterfaceConfiguration(1)
        {
            ConfigurationMode = IpConfigurationMode.AutomaticDhcp
        };

        // Act
        var proposed = original.Clone();
        proposed.UpdateFrom(targetLocalNetwork);
        proposed.UpdateFrom(targetInternetConnection);
        proposed.NetworkDNSSettings.Hostname = "suite-host";

        // Assert
        original.Equals(proposed).Should().BeFalse(
            "Changing from DHCP to manual should make configurations unequal");
    }

    [Fact]
    public void SystemConfiguration_Equals_should_handle_services_list_correctly()
    {
        // Arrange
        var config1 = new SystemConfiguration([new ServiceDetail { Name = "suite", State = ServiceState.Enabled }])
        {
            NetworkInterfacesSettings = new NetworkInterfacesSettings(),
            NetworkDNSSettings = new NetworkDNSSettings(),
            NetworkProxySettings = new NetworkProxySettings(),
            NetworkNTPSettings = new NetworkNTPSettings()
        };

        var config2 = new SystemConfiguration([new ServiceDetail { Name = "suite", State = ServiceState.Enabled }])
        {
            NetworkInterfacesSettings = new NetworkInterfacesSettings(),
            NetworkDNSSettings = new NetworkDNSSettings(),
            NetworkProxySettings = new NetworkProxySettings(),
            NetworkNTPSettings = new NetworkNTPSettings()
        };

        // Act & Assert
        config1.Equals(config2).Should().BeTrue();
    }

    [Fact]
    public void SystemConfiguration_Clone_with_services_should_remain_equal()
    {
        // Arrange
        var original = new SystemConfiguration([new ServiceDetail { Name = "suite", State = ServiceState.Enabled }])
        {
            NetworkInterfacesSettings = new NetworkInterfacesSettings(),
            NetworkDNSSettings = new NetworkDNSSettings(),
            NetworkProxySettings = new NetworkProxySettings(),
            NetworkNTPSettings = new NetworkNTPSettings()
        };

        // Act
        var clone = original.Clone();

        // Assert
        original.Equals(clone).Should().BeTrue("Clone should preserve services equality");
    }
}
