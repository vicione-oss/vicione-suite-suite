using System.Net.NetworkInformation;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using Core.Shared.HostManagement.Requests;

namespace Blazor.Shared.Tests.Network.ControlPanels.NetworkInterface.Services;

public sealed class NetworkInterfaceHardwareControlPanelResetHandlerTests : NetworkInterfaceControlPanelResetHandlerTestsBase
{
    private protected override NetworkInterfaceControlPanelResetHandlerBase CreateResetHandler()
        => new NetworkInterfaceHardwareControlPanelResetHandler(Mediator, SystemConfigurationService);

    [Fact]
    public async Task Should_reset_to_defaults_when_network_interface_not_found()
    {
        // Arrange
        SetupSystemConfiguration();
        var state = CreateEditedState();
        state.NetworkInterfaceIndex = 5;
        state.OriginalPhysicalAddress = "11:22:33:44:55:66";

        // Act
        await CreateResetHandler().Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.MacAddressManually.Should().BeFalse();
        state.MacAddress.Should().BeEmpty();
        state.OriginalPhysicalAddress.Should().BeNull();
    }

    [Fact]
    public async Task Should_set_mac_address_manually_when_user_defined_mac_address_is_enabled()
    {
        // Arrange
        const string customMac = "AA:BB:CC:DD:EE:FF";
        var networkInterface = CreateNetworkInterface();
        networkInterface.CommonInformation.UserDefinedMACAddress = new() { Enabled = true, Address = PhysicalAddress.Parse(customMac) };
        SetupSystemConfiguration(networkInterface);
        var state = new NetworkInterfaceControlPanelState();

        // Act
        await CreateResetHandler().Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.MacAddressManually.Should().BeTrue();
        state.MacAddress.Should().Be(customMac);
    }

    [Fact]
    public async Task Should_not_set_mac_address_manually_when_user_defined_mac_address_is_disabled()
    {
        // Arrange
        var networkInterface = CreateNetworkInterface();
        networkInterface.CommonInformation.UserDefinedMACAddress = new() { Enabled = false, Address = PhysicalAddress.Parse("AA:BB:CC:DD:EE:FF") };
        SetupSystemConfiguration(networkInterface);
        var state = new NetworkInterfaceControlPanelState();

        // Act
        await CreateResetHandler().Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.MacAddressManually.Should().BeFalse();
        state.MacAddress.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_load_original_mac_address_of_network_interface()
    {
        // Arrange
        const string originalMac = "11:22:33:44:55:66";
        SetupSystemConfiguration(CreateNetworkInterface());

        Mediator.Request<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>(
                Arg.Any<GetOriginalPhysicalAddress>(), Arg.Any<CancellationToken>())
            .Returns(new GetOriginalPhysicalAddressResponse { OriginalPhysicalAddress = originalMac });

        var state = new NetworkInterfaceControlPanelState();

        // Act
        await CreateResetHandler().Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.OriginalPhysicalAddress.Should().Be(originalMac);
    }

    [Fact]
    public async Task Should_keep_original_mac_address_separate_from_user_defined_mac_address_when_enabled()
    {
        // Arrange
        const string originalMac = "11:22:33:44:55:66";
        var networkInterface = CreateNetworkInterface();
        networkInterface.CommonInformation.UserDefinedMACAddress = new() { Enabled = true, Address = PhysicalAddress.Parse("AA:BB:CC:DD:EE:FF") };
        SetupSystemConfiguration(networkInterface);

        Mediator.Request<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>(
                Arg.Any<GetOriginalPhysicalAddress>(), Arg.Any<CancellationToken>())
            .Returns(new GetOriginalPhysicalAddressResponse { OriginalPhysicalAddress = originalMac });

        var state = new NetworkInterfaceControlPanelState();

        // Act
        await CreateResetHandler().Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.MacAddress.Should().Be("AA:BB:CC:DD:EE:FF");
        state.OriginalPhysicalAddress.Should().Be(originalMac);
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
        state.MacAddressManually.Should().BeFalse();
        state.HardwareHasChanges.Should().BeFalse();

        state.Enabled.Should().BeFalse();
        state.IpV4ConfigurationMode.Should().Be(IpConfigurationMode.Manual);
        state.VLanId.Should().Be("42");
        state.GeneralHasChanges.Should().BeTrue();
        state.TcpIpHasChanges.Should().BeTrue();
    }
}
