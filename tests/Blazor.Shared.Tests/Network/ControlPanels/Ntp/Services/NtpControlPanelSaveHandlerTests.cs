using Blazor.Shared.Network.ControlPanels.Ntp.Models;
using Blazor.Shared.Network.ControlPanels.Ntp.Services;
using Blazor.Shared.Services;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Events;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;
using Microsoft.Extensions.Logging.Abstractions;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Sdk.SystemConfiguration.Events;

namespace Blazor.Shared.Tests.Network.ControlPanels.Ntp.Services;

public sealed class NtpControlPanelSaveHandlerTests
{
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly ISystemConfigurationService _systemConfigurationService = Substitute.For<ISystemConfigurationService>();

    private NtpControlPanelSaveHandler CreateHandler() =>
        new(_mediator, _systemConfigurationService, NullLogger<NtpControlPanelSaveHandler>.Instance);

    private void SetupSystemConfiguration(NetworkNTPSettings? ntpSettings = null)
    {
        _systemConfigurationService.SystemConfiguration.Returns(new SystemConfiguration
        {
            NetworkInterfaces = [new NetworkInterfaceDetail
            {
                CommonInformation = new NetworkInterfaceCommonInformation { Name = "eth0", Enabled = true },
                IPv4 = new IPv4Settings
                {
                    IPv4Details = [new IPv4Detail
                    {
                        IPAddress = System.Net.IPAddress.Parse("192.168.1.10"),
                        Netmask = System.Net.IPAddress.Parse("255.255.255.0")
                    }],
                    Gateway = System.Net.IPAddress.Parse("192.168.1.1")
                }
            }],
            NetworkDNSSettings = new NetworkDNSSettings { Hostname = "test-host" },
            NetworkNTPSettings = ntpSettings ?? new NetworkNTPSettings()
        });
    }

    private static void FireSuccessEvent(NtpControlPanelSaveHandler handler, SetSystemConfiguration command)
    {
        var @event = new SystemConfigurationChanged { CorrelationId = command.CorrelationId };
        _ = handler.Consume(new ClientContext<SystemConfigurationChanged>(@event, command.CorrelationId), CancellationToken.None);
    }

    private void SetupMediatorToFireSuccess(NtpControlPanelSaveHandler handler, Action<SetSystemConfiguration>? onSend = null)
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

    private void SetupMediatorToFireError(NtpControlPanelSaveHandler handler, ErrorInfo error)
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

    private static NtpControlPanelState CreateValidState()
    {
        var state = new NtpControlPanelState { NtpServersEnabled = false };
        return state;
    }

    [Fact]
    public async Task Should_return_success_when_save_succeeds()
    {
        // Arrange
        SetupSystemConfiguration();
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
        SetupSystemConfiguration();
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
    public async Task Should_save_ntp_servers_enabled_true_from_state()
    {
        // Arrange
        SetupSystemConfiguration();
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.NtpServersEnabled = true;
        state.NtpServerDetails.Add(new NtpServerDetail { IpAddressOrHostname = "pool.ntp.org" });

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.NetworkNTPSettings.Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task Should_save_ntp_servers_enabled_false_from_state()
    {
        // Arrange
        SetupSystemConfiguration();
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.NtpServersEnabled = false;

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.NetworkNTPSettings.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task Should_save_ntp_servers_from_state()
    {
        // Arrange
        SetupSystemConfiguration();
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.NtpServersEnabled = true;
        state.NtpServerDetails.Add(new NtpServerDetail { IpAddressOrHostname = "pool.ntp.org" });
        state.NtpServerDetails.Add(new NtpServerDetail { IpAddressOrHostname = "time.cloudflare.com" });

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        var ntp = captured!.NetworkNTPSettings;
        ntp.Servers.Should().HaveCount(2);
        ntp.Servers[0].Should().Be("pool.ntp.org");
        ntp.Servers[1].Should().Be("time.cloudflare.com");
    }

    [Fact]
    public async Task Should_filter_out_empty_ntp_servers()
    {
        // Arrange
        SetupSystemConfiguration();
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.NtpServersEnabled = true;
        state.NtpServerDetails.Add(new NtpServerDetail { IpAddressOrHostname = "pool.ntp.org" });
        state.NtpServerDetails.Add(new NtpServerDetail { IpAddressOrHostname = string.Empty });

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.NetworkNTPSettings.Servers.Should().HaveCount(1);
        captured.NetworkNTPSettings.Servers[0].Should().Be("pool.ntp.org");
    }

    [Fact]
    public async Task Should_preserve_network_interfaces_settings_from_system_configuration()
    {
        // Arrange
        SetupSystemConfiguration();
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.NetworkInterfaces.Should().Equal(_systemConfigurationService.SystemConfiguration.NetworkInterfaces);
    }

    [Fact]
    public async Task Should_preserve_dns_settings_from_system_configuration()
    {
        // Arrange
        SetupSystemConfiguration();
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.NetworkDNSSettings.Should().Be(_systemConfigurationService.SystemConfiguration.NetworkDNSSettings);
    }
}
