using Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using Core.Shared.HostManagement.Requests;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Tests.Network.ControlPanels.NetworkInterface.Services;

public abstract class NetworkInterfaceControlPanelResetHandlerTestsBase
{
    private protected IUiMediator Mediator { get; } = Substitute.For<IUiMediator>();
    private protected ISystemConfigurationService SystemConfigurationService { get; } = Substitute.For<ISystemConfigurationService>();

    protected NetworkInterfaceControlPanelResetHandlerTestsBase()
    {
        Mediator.Request<GetDHCPLeaseInformation, GetDHCPLeaseInformationResponse>(
                Arg.Any<GetDHCPLeaseInformation>(), Arg.Any<CancellationToken>())
            .Returns(new GetDHCPLeaseInformationResponse());

        Mediator.Request<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>(
                Arg.Any<GetOriginalPhysicalAddress>(), Arg.Any<CancellationToken>())
            .Returns(new GetOriginalPhysicalAddressResponse());
    }

    private protected abstract NetworkInterfaceControlPanelResetHandlerBase CreateResetHandler();

    private protected void SetupSystemConfiguration(params NetworkInterfaceDetail[] networkInterfaces)
    {
        SystemConfigurationService.SystemConfiguration.Returns(new SystemConfiguration
        {
            NetworkInterfaces = [.. networkInterfaces]
        });
    }

    private protected static NetworkInterfaceDetail CreateNetworkInterface(IPv4Settings? ipV4 = null) => new()
    {
        CommonInformation = new NetworkInterfaceCommonInformation { Name = "eth0", Enabled = true },
        IPv4 = ipV4 ?? new IPv4Settings()
    };

    /// <summary>
    /// A state with edits on every tab, so a test can check which of them a reset leaves untouched.
    /// </summary>
    private protected static NetworkInterfaceControlPanelState CreateEditedState() => new()
    {
        NetworkInterfaceIndex = 0,
        Enabled = false,
        IpV4ConfigurationMode = IpConfigurationMode.Manual,
        FirstIpV4Detail = new() { IpAddress = "192.168.99.10", SubnetMask = "255.255.255.0" },
        DefaultGateway = "192.168.99.1",
        VLanEnabled = true,
        VLanId = "42",
        MacAddressManually = true,
        MacAddress = "AA:BB:CC:DD:EE:FF",
        GeneralHasChanges = true,
        TcpIpHasChanges = true,
        HardwareHasChanges = true
    };

    [Fact]
    public async Task Should_end_loading_after_reset()
    {
        // Arrange
        SetupSystemConfiguration();
        var state = new NetworkInterfaceControlPanelState();

        // Act
        await CreateResetHandler().Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task Should_load_the_interface_name()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());
        var state = new NetworkInterfaceControlPanelState();

        // Act
        await CreateResetHandler().Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.Name.Should().Be("eth0");
    }
}
