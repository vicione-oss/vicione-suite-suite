using System.Net.NetworkInformation;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Events;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;
using Microsoft.Extensions.Logging.Abstractions;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Sdk.SystemConfiguration.Events;

namespace Blazor.Shared.Tests.Network.ControlPanels.NetworkInterface.Services;

public sealed class NetworkInterfaceSaverTests
{
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly ISystemConfigurationService _systemConfigurationService = Substitute.For<ISystemConfigurationService>();

    private NetworkInterfaceSaver CreateHandler() =>
        new(_mediator, _systemConfigurationService, NullLogger<NetworkInterfaceSaver>.Instance);

    private void SetupSystemConfiguration(params NetworkInterfaceDetail[] networkInterfaces)
    {
        _systemConfigurationService.SystemConfiguration.Returns(new SystemConfiguration
        {
            NetworkInterfaces = [.. networkInterfaces],
            NetworkDNSSettings = new NetworkDNSSettings { Hostname = "test-host" }
        });
    }

    private static void FireSuccessEvent(NetworkInterfaceSaver handler, SetSystemConfiguration command)
    {
        var @event = new SystemConfigurationChanged { CorrelationId = command.CorrelationId };
        _ = handler.Consume(new ClientContext<SystemConfigurationChanged>(@event, command.CorrelationId), CancellationToken.None);
    }

    private void SetupMediatorToFireSuccess(NetworkInterfaceSaver handler, Action<SetSystemConfiguration>? onSend = null)
    {
        _mediator
            .When(m => m.Send(Arg.Any<SetSystemConfiguration>(), Arg.Any<CancellationToken>()))
            .Do(call =>
            {
                var command = call.Arg<SetSystemConfiguration>();
                onSend?.Invoke(command!);
                FireSuccessEvent(handler, command!);
            });
    }

    private void SetupMediatorToFireError(NetworkInterfaceSaver handler, ErrorInfo error)
    {
        _mediator
            .When(m => m.Send(Arg.Any<SetSystemConfiguration>(), Arg.Any<CancellationToken>()))
            .Do(call =>
            {
                var command = call.Arg<SetSystemConfiguration>();
                var @event = new SetSystemConfigurationError(command!.CorrelationId, error);
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
        DefaultGateway = "192.168.1.1",
        GeneralHasChanges = true,
        TcpIpHasChanges = true,
        HardwareHasChanges = true
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
            () => handler.SaveChanges(state, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_return_success_when_save_succeeds()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());

        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler);

        var state = CreateValidState();

        // Act
        var result = await handler.SaveChanges(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
    }

    [Fact]
    public async Task Should_return_error_result_when_set_system_configuration_fails()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());

        using var handler = CreateHandler();
        var error = new ErrorInfo(42, "Something went wrong");
        SetupMediatorToFireError(handler, error);

        var state = CreateValidState();

        // Act
        var result = await handler.SaveChanges(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
        ((SaveErrorResult)result).Message.Should().Be("Something went wrong");
    }

    [Fact]
    public async Task Should_set_dhcp_enabled_true_when_mode_is_automatic_dhcp()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());

        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.IpV4ConfigurationMode = IpConfigurationMode.AutomaticDhcp;
        state.FirstIpV4Detail = new() { IpAddress = string.Empty, SubnetMask = string.Empty };

        // Act
        await handler.SaveChanges(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.NetworkInterfaces[0].IPv4.DHCPEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Should_set_dhcp_enabled_false_when_mode_is_manual()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());

        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.IpV4ConfigurationMode = IpConfigurationMode.Manual;

        // Act
        await handler.SaveChanges(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.NetworkInterfaces[0].IPv4.DHCPEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Should_use_custom_mac_address_when_mac_address_manually_set()
    {
        // Arrange
        const string customMac = "AA:BB:CC:DD:EE:FF";
        SetupSystemConfiguration(CreateNetworkInterface());

        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.MacAddressManually = true;
        state.MacAddress = customMac;

        // Act
        await handler.SaveChanges(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.NetworkInterfaces[0].CommonInformation.UserDefinedMACAddress.Should().Be(new UserDefinedMACAddressSettings { Enabled = true, Address = PhysicalAddress.Parse(customMac) });
    }

    [Theory]
    [InlineData("AA:BB:CC:00:11:22")]
    [InlineData("aa-bb-cc-00-11-22")]
    [InlineData("AABBCC001122")]
    public async Task Should_disable_user_defined_mac_address_when_manual_mac_equals_original_physical_address(string manualMac)
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());

        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.OriginalPhysicalAddress = "AA:BB:CC:00:11:22";
        state.MacAddressManually = true;
        state.MacAddress = manualMac;

        // Act
        var result = await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        captured!.NetworkInterfaces[0].CommonInformation.UserDefinedMACAddress.Should().Be(new UserDefinedMACAddressSettings());
    }

    [Fact]
    public async Task Should_turn_off_manual_mac_address_in_state_when_it_equals_original_physical_address()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());

        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler);

        var state = CreateValidState();
        state.OriginalPhysicalAddress = "AA:BB:CC:00:11:22";
        state.MacAddressManually = true;
        state.MacAddress = "AA:BB:CC:00:11:22";

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        state.MacAddressManually.Should().BeFalse();
        state.MacAddress.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_use_custom_mac_address_when_original_physical_address_is_unknown()
    {
        // Arrange
        const string customMac = "AA:BB:CC:00:11:22";
        SetupSystemConfiguration(CreateNetworkInterface());

        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.OriginalPhysicalAddress = null;
        state.MacAddressManually = true;
        state.MacAddress = customMac;

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.NetworkInterfaces[0].CommonInformation.UserDefinedMACAddress.Should().Be(new UserDefinedMACAddressSettings { Enabled = true, Address = PhysicalAddress.Parse(customMac) });
    }

    [Fact]
    public async Task Should_disable_user_defined_mac_address_when_mac_not_set_manually()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());

        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.MacAddressManually = false;
        state.MacAddress = string.Empty;

        // Act
        await handler.SaveChanges(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.NetworkInterfaces[0].CommonInformation.UserDefinedMACAddress.Should().Be(new UserDefinedMACAddressSettings());
    }

    [Fact]
    public async Task Should_return_error_result_when_mac_address_manually_set_but_empty()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());

        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler);

        var state = CreateValidState();
        state.MacAddressManually = true;
        state.MacAddress = string.Empty;

        // Act
        var result = await handler.SaveChanges(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
    }

    [Fact]
    public async Task Should_set_gateway_to_null_when_default_gateway_is_empty()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());

        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.DefaultGateway = null;

        // Act
        await handler.SaveChanges(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.NetworkInterfaces[0].IPv4.Gateway.Should().BeNull();
    }

    [Fact]
    public async Task Should_set_gateway_when_default_gateway_provided()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());

        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.DefaultGateway = "192.168.1.254";

        // Act
        await handler.SaveChanges(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.NetworkInterfaces[0].IPv4.Gateway.Should().Be(System.Net.IPAddress.Parse("192.168.1.254"));
    }

    [Fact]
    public async Task Should_set_vlan_settings_when_vlan_enabled()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());

        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.VLanEnabled = true;
        state.VLanId = "100";

        // Act
        await handler.SaveChanges(state, TestContext.Current.CancellationToken);

        // Assert
        var iface = captured!.NetworkInterfaces[0];
        iface.VLAN.Enabled.Should().BeTrue();
        iface.VLAN.ID.Should().Be(100);
    }

    [Fact]
    public async Task Should_return_error_result_when_vlan_id_is_not_an_integer()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());

        using var handler = CreateHandler();

        var state = CreateValidState();
        state.VLanEnabled = true;
        state.VLanId = "not-a-number";

        // Act
        var result = await handler.SaveChanges(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
    }

    [Fact]
    public async Task Should_return_error_result_when_mac_address_is_not_a_mac_address()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());

        using var handler = CreateHandler();

        var state = CreateValidState();
        state.MacAddressManually = true;
        state.MacAddress = "not-a-mac";

        // Act
        var result = await handler.SaveChanges(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>()
            .Which.Message.Should().Contain("not-a-mac");
    }

    [Fact]
    public async Task Should_save_multiple_ipv4_details()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());

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
        await handler.SaveChanges(state, TestContext.Current.CancellationToken);

        // Assert
        var ipDetails = captured!.NetworkInterfaces[0].IPv4.IPv4Details;
        ipDetails.Should().HaveCount(2);
        ipDetails[0].IPAddress.Should().Be(System.Net.IPAddress.Parse("192.168.1.10"));
        ipDetails[1].IPAddress.Should().Be(System.Net.IPAddress.Parse("10.0.0.1"));
    }

    [Fact]
    public async Task Should_send_one_set_system_configuration_for_parallel_saves_of_the_same_interface()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());

        using var handler = CreateHandler();
        var commands = new List<SetSystemConfiguration>();
        _mediator
            .When(m => m.Send(Arg.Any<SetSystemConfiguration>(), Arg.Any<CancellationToken>()))
            .Do(call => commands.Add(call.Arg<SetSystemConfiguration>()));

        var state = CreateValidState();

        // Act
        var saves = new[]
        {
            handler.SaveChanges(state, TestContext.Current.CancellationToken),
            handler.SaveChanges(state, TestContext.Current.CancellationToken),
            handler.SaveChanges(state, TestContext.Current.CancellationToken)
        };

        FireSuccessEvent(handler, commands.Single());
        var results = await Task.WhenAll(saves);

        // Assert
        commands.Should().ContainSingle();
        results.Should().AllBeOfType<SaveSuccessResult>();
    }

    [Fact]
    public async Task Should_keep_the_stored_settings_of_tabs_without_changes()
    {
        // Arrange
        const string storedMac = "AA:BB:CC:DD:EE:FF";
        var networkInterface = CreateNetworkInterface();
        networkInterface.CommonInformation.UserDefinedMACAddress = new UserDefinedMACAddressSettings { Enabled = true, Address = PhysicalAddress.Parse(storedMac) };
        networkInterface.IPv4.DHCPEnabled = true;
        networkInterface.VLAN.Enabled = true;
        networkInterface.VLAN.ID = 7;
        SetupSystemConfiguration(networkInterface);

        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.TcpIpHasChanges = false;
        state.HardwareHasChanges = false;
        state.Enabled = false;

        // Act
        await handler.SaveChanges(state, TestContext.Current.CancellationToken);

        // Assert
        var savedInterface = captured!.NetworkInterfaces[0];
        savedInterface.CommonInformation.Enabled.Should().BeFalse();
        savedInterface.CommonInformation.UserDefinedMACAddress.Should().Be(networkInterface.CommonInformation.UserDefinedMACAddress);
        savedInterface.IPv4.Should().BeSameAs(networkInterface.IPv4);
        savedInterface.VLAN.Should().BeSameAs(networkInterface.VLAN);
    }

    [Fact]
    public async Task Should_not_change_the_stored_system_configuration()
    {
        // Arrange
        var networkInterface = CreateNetworkInterface();
        SetupSystemConfiguration(networkInterface);

        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler);

        var state = CreateValidState();
        state.Enabled = false;

        // Act
        await handler.SaveChanges(state, TestContext.Current.CancellationToken);

        // Assert
        networkInterface.CommonInformation.Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task Should_clear_the_change_flags_of_the_saved_tabs_on_success()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());

        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler);

        var state = CreateValidState();

        // Act
        await handler.SaveChanges(state, TestContext.Current.CancellationToken);

        // Assert
        state.GeneralHasChanges.Should().BeFalse();
        state.TcpIpHasChanges.Should().BeFalse();
        state.HardwareHasChanges.Should().BeFalse();
    }

    [Fact]
    public async Task Should_keep_the_change_flags_when_set_system_configuration_fails()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());

        using var handler = CreateHandler();
        SetupMediatorToFireError(handler, new ErrorInfo(42, "Something went wrong"));

        var state = CreateValidState();

        // Act
        await handler.SaveChanges(state, TestContext.Current.CancellationToken);

        // Assert
        state.GeneralHasChanges.Should().BeTrue();
        state.TcpIpHasChanges.Should().BeTrue();
        state.HardwareHasChanges.Should().BeTrue();
    }

    [Fact]
    public async Task Should_return_success_without_sending_when_no_tab_has_changes()
    {
        // Arrange
        SetupSystemConfiguration(CreateNetworkInterface());

        using var handler = CreateHandler();

        var state = CreateValidState();
        state.GeneralHasChanges = false;
        state.TcpIpHasChanges = false;
        state.HardwareHasChanges = false;

        // Act
        var result = await handler.SaveChanges(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        await _mediator.DidNotReceive().Send(Arg.Any<SetSystemConfiguration>(), Arg.Any<CancellationToken>());
    }
}
