using System.Net.NetworkInformation;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Extensions;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;
using Blazor.Shared.Services;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;
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
        var state = new NetworkInterfaceControlPanelState { NetworkInterfaceIndex = 0, OriginalMacAddress = OriginalMac };

        // Act
        var physicalAddress = state.GetPhysicalAddress(_systemConfigurationService);

        // Assert
        physicalAddress.Should().Be("AA:BB:CC:DD:EE:FF");
    }

    [Fact]
    public void Should_return_original_mac_address_when_user_defined_mac_address_is_disabled()
    {
        // Arrange
        SetupSystemConfiguration(new UserDefinedMACAddressSettings());
        var state = new NetworkInterfaceControlPanelState { NetworkInterfaceIndex = 0, OriginalMacAddress = OriginalMac };

        // Act
        var physicalAddress = state.GetPhysicalAddress(_systemConfigurationService);

        // Assert
        physicalAddress.Should().Be(OriginalMac);
    }

    [Fact]
    public void Should_return_unknown_when_user_defined_mac_address_is_disabled_and_original_mac_address_is_unknown()
    {
        // Arrange
        SetupSystemConfiguration(new UserDefinedMACAddressSettings());
        var state = new NetworkInterfaceControlPanelState { NetworkInterfaceIndex = 0 };

        // Act
        var physicalAddress = state.GetPhysicalAddress(_systemConfigurationService);

        // Assert
        physicalAddress.Should().Be(CommonVocabulary.Unknown);
    }

    [Fact]
    public void Should_return_unknown_when_network_interface_is_not_found()
    {
        // Arrange
        SetupSystemConfiguration(new UserDefinedMACAddressSettings());
        var state = new NetworkInterfaceControlPanelState { NetworkInterfaceIndex = 5, OriginalMacAddress = OriginalMac };

        // Act
        var physicalAddress = state.GetPhysicalAddress(_systemConfigurationService);

        // Assert
        physicalAddress.Should().Be(CommonVocabulary.Unknown);
    }
}
