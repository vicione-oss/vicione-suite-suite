using AwesomeAssertions;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Events;
using Core.Shared.HostManagement.Requests;
using Core.Shared.HostManagement.Services;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Sdk.SystemConfiguration.Events;
using Xunit;

namespace Blazor.Shared.Tests.Network.ControlPanels.NetworkInterface.Services;

public sealed class NetworkInterfaceControlPanelSaveHandlerTests
{
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly ISystemConfigurationService _systemConfigurationService = Substitute.For<ISystemConfigurationService>();

    private NetworkInterfaceControlPanelSaveHandler CreateHandler() =>
        new(_mediator, _systemConfigurationService, NullLogger<NetworkInterfaceControlPanelSaveHandler>.Instance);

    private void SetupSystemConfiguration(params NetworkInterfaceDetail[] networkInterfaces)
    {
        _systemConfigurationService.SystemConfiguration.Returns(new SystemConfiguration
        {
            NetworkInterfacesSettings = new NetworkInterfacesSettings([.. networkInterfaces]),
            NetworkDNSSettings = new NetworkDNSSettings { Hostname = "test-host" }
        });
    }

    private static void FireSuccessEvent(NetworkInterfaceControlPanelSaveHandler handler, SetSystemConfiguration command)
    {
        var @event = new SystemConfigurationChanged { CorrelationId = command.CorrelationId };
        _ = handler.Consume(new ClientContext<SystemConfigurationChanged>(@event, command.CorrelationId), CancellationToken.None);
    }

    private void SetupMediatorToFireSuccess(NetworkInterfaceControlPanelSaveHandler handler, Action<SetSystemConfiguration>? onSend = null)
    {
        _mediator
            .When(m => m.Send(Arg.Any<SetSystemConfiguration>(), Arg.Any<CancellationToken>()))
            .Do(call =>
            {
                var command = call.Arg<SetSystemConfiguration>();
                onSend?.Invoke(command);
                FireSuccessEvent(handler, command);
            });
    }

    private void SetupMediatorToFireError(NetworkInterfaceControlPanelSaveHandler handler, ErrorInfo error)
    {
        _mediator
            .When(m => m.Send(Arg.Any<SetSystemConfiguration>(), Arg.Any<CancellationToken>()))
            .Do(call =>
            {
                var command = call.Arg<SetSystemConfiguration>();
                var @event = new SetSystemConfigurationError(command.CorrelationId, error);
                _ = handler.Consume(new ClientContext<SetSystemConfigurationError>(@event, command.CorrelationId), CancellationToken.None);
            });
    }

    private static NetworkInterfaceControlPanelState CreateValidState(int index = 0) => new()
    {
        NetworkInterfaceIndex = index,
        Name = "eth0",
        Enabled = true,
        IpV4ConfigurationMode = IpConfigurationMode.Manual,
        FirstIpV4Detail = new() { IpAddress = "192.168.1.10", SubnetMask = "255.255.255.0" },
        DefaultGateway = "192.168.1.1"
    };

    private static NetworkInterfaceDetail CreateNetworkInterface(string name = "eth0") => new()
    {
        CommonInformation = new NetworkInterfaceCommonInformation { Name = name, Enabled = true },
        IPv4 = new IPv4Settings()
    };

    [Fact]
    public async Task Should_return_error_result_when_network_interface_index_out_of_range()
    {
        // Arrange
        SetupSystemConfiguration(); // no interfaces
        using var handler = CreateHandler();
        var state = CreateValidState(index: 5);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => handler.Save(state, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_return_success_when_save_succeeds()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());
        _mediator.Request<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>(
                Arg.Any<GetOriginalPhysicalAddress>(), Arg.Any<CancellationToken>())
            .Returns(new GetOriginalPhysicalAddressResponse());

        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler);

        var state = CreateValidState();

        // Act
        var result = await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
    }

    [Fact]
    public async Task Should_return_error_result_when_set_system_configuration_fails()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());
        _mediator.Request<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>(
                Arg.Any<GetOriginalPhysicalAddress>(), Arg.Any<CancellationToken>())
            .Returns(new GetOriginalPhysicalAddressResponse());

        using var handler = CreateHandler();
        var error = new ErrorInfo(42, "Something went wrong");
        SetupMediatorToFireError(handler, error);

        var state = CreateValidState();

        // Act
        var result = await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
        ((SaveErrorResult)result).Message.Should().Be("Something went wrong");
    }

    [Fact]
    public async Task Should_set_dhcp_enabled_true_when_mode_is_automatic_dhcp()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());
        _mediator.Request<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>(
                Arg.Any<GetOriginalPhysicalAddress>(), Arg.Any<CancellationToken>())
            .Returns(new GetOriginalPhysicalAddressResponse());

        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.IpV4ConfigurationMode = IpConfigurationMode.AutomaticDhcp;
        state.FirstIpV4Detail = new() { IpAddress = string.Empty, SubnetMask = string.Empty };

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.NetworkInterfacesSettings.NetworkInterfaces[0].IPv4.DHCPEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Should_set_dhcp_enabled_false_when_mode_is_manual()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());
        _mediator.Request<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>(
                Arg.Any<GetOriginalPhysicalAddress>(), Arg.Any<CancellationToken>())
            .Returns(new GetOriginalPhysicalAddressResponse());

        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.IpV4ConfigurationMode = IpConfigurationMode.Manual;

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.NetworkInterfacesSettings.NetworkInterfaces[0].IPv4.DHCPEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Should_use_custom_mac_address_when_mac_address_manually_set()
    {
        // Arrange
        const string customMac = "AA:BB:CC:DD:EE:FF";
        SetupSystemConfiguration(CreateNetworkInterface());
        _mediator.Request<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>(
                Arg.Any<GetOriginalPhysicalAddress>(), Arg.Any<CancellationToken>())
            .Returns(new GetOriginalPhysicalAddressResponse { OriginalPhysicalAddress = "11:22:33:44:55:66" });

        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.MacAddressManually = true;
        state.MacAddress = customMac;

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.NetworkInterfacesSettings.NetworkInterfaces[0].CommonInformation.PhysicalAddress.Should().Be(customMac);
    }

    [Fact]
    public async Task Should_use_original_mac_address_when_mac_not_set_manually()
    {
        // Arrange
        const string originalMac = "AA:BB:CC:11:22:33";
        SetupSystemConfiguration(CreateNetworkInterface());
        _mediator.Request<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>(
                Arg.Any<GetOriginalPhysicalAddress>(), Arg.Any<CancellationToken>())
            .Returns(new GetOriginalPhysicalAddressResponse { OriginalPhysicalAddress = originalMac });

        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.MacAddressManually = false;
        state.MacAddress = string.Empty;

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.NetworkInterfacesSettings.NetworkInterfaces[0].CommonInformation.PhysicalAddress.Should().Be(originalMac);
    }

    [Fact]
    public async Task Should_set_gateway_to_null_when_default_gateway_is_empty()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());
        _mediator.Request<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>(
                Arg.Any<GetOriginalPhysicalAddress>(), Arg.Any<CancellationToken>())
            .Returns(new GetOriginalPhysicalAddressResponse());

        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.DefaultGateway = null;

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.NetworkInterfacesSettings.NetworkInterfaces[0].IPv4.Gateway.Should().BeNull();
    }

    [Fact]
    public async Task Should_set_gateway_when_default_gateway_provided()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());
        _mediator.Request<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>(
                Arg.Any<GetOriginalPhysicalAddress>(), Arg.Any<CancellationToken>())
            .Returns(new GetOriginalPhysicalAddressResponse());

        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.DefaultGateway = "192.168.1.254";

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.NetworkInterfacesSettings.NetworkInterfaces[0].IPv4.Gateway.Should().Be(System.Net.IPAddress.Parse("192.168.1.254"));
    }

    [Fact]
    public async Task Should_set_vlan_settings_when_vlan_enabled()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());
        _mediator.Request<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>(
                Arg.Any<GetOriginalPhysicalAddress>(), Arg.Any<CancellationToken>())
            .Returns(new GetOriginalPhysicalAddressResponse());

        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.VLanEnabled = true;
        state.VLanId = "100";

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        var iface = captured!.NetworkInterfacesSettings.NetworkInterfaces[0];
        iface.IPv4.VLANEnabled.Should().BeTrue();
        iface.IPv4.VLANID.Should().Be(100);
    }

    [Fact]
    public async Task Should_return_error_result_when_vlan_id_is_not_an_integer()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());
        _mediator.Request<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>(
                Arg.Any<GetOriginalPhysicalAddress>(), Arg.Any<CancellationToken>())
            .Returns(new GetOriginalPhysicalAddressResponse());

        using var handler = CreateHandler();

        var state = CreateValidState();
        state.VLanEnabled = true;
        state.VLanId = "not-a-number";

        // Act
        var result = await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
    }

    [Fact]
    public async Task Should_save_multiple_ipv4_details()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());
        _mediator.Request<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>(
                Arg.Any<GetOriginalPhysicalAddress>(), Arg.Any<CancellationToken>())
            .Returns(new GetOriginalPhysicalAddressResponse());

        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.FirstIpV4Detail = new() { IpAddress = "192.168.1.10", SubnetMask = "255.255.255.0" };
        state.AdditionalIpV4Details =
        [
            new() { IpAddress = "10.0.0.1", SubnetMask = "255.0.0.0" }
        ];

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        var ipDetails = captured!.NetworkInterfacesSettings.NetworkInterfaces[0].IPv4.IPv4Details;
        ipDetails.Should().HaveCount(2);
        ipDetails[0].IPAddress.Should().Be(System.Net.IPAddress.Parse("192.168.1.10"));
        ipDetails[1].IPAddress.Should().Be(System.Net.IPAddress.Parse("10.0.0.1"));
    }

    [Fact]
    public async Task Should_call_set_system_configuration_service_on_success()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());
        _mediator.Request<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>(
                Arg.Any<GetOriginalPhysicalAddress>(), Arg.Any<CancellationToken>())
            .Returns(new GetOriginalPhysicalAddressResponse());

        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler);

        var state = CreateValidState();

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        await _systemConfigurationService.Received(1).SetSystemConfiguration(Arg.Any<SystemConfiguration>());
    }

    [Fact]
    public async Task Should_not_call_set_system_configuration_service_on_error()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());
        _mediator.Request<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>(
                Arg.Any<GetOriginalPhysicalAddress>(), Arg.Any<CancellationToken>())
            .Returns(new GetOriginalPhysicalAddressResponse());

        using var handler = CreateHandler();
        SetupMediatorToFireError(handler, new ErrorInfo(1, "Error"));

        var state = CreateValidState();

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        await _systemConfigurationService.DidNotReceive().SetSystemConfiguration(Arg.Any<SystemConfiguration>());
    }
}
