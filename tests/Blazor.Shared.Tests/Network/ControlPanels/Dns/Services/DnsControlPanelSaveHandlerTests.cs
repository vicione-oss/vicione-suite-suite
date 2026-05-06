using System.Net;
using AwesomeAssertions;
using Blazor.Shared.Network.ControlPanels.Dns.Models;
using Blazor.Shared.Network.ControlPanels.Dns.Services;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Events;
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

namespace Blazor.Shared.Tests.Network.ControlPanels.Dns.Services;

public sealed class DnsControlPanelSaveHandlerTests
{
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly ISystemConfigurationService _systemConfigurationService = Substitute.For<ISystemConfigurationService>();

    private DnsControlPanelSaveHandler CreateHandler() =>
        new(_mediator, _systemConfigurationService, NullLogger<DnsControlPanelSaveHandler>.Instance);

    private void SetupSystemConfiguration(NetworkDNSSettings? dnsSettings = null)
    {
        _systemConfigurationService.SystemConfiguration.Returns(new SystemConfiguration
        {
            NetworkInterfacesSettings = new NetworkInterfacesSettings([new NetworkInterfaceDetail
            {
                CommonInformation = new NetworkInterfaceCommonInformation { Name = "eth0", Enabled = true },
                IPv4 = new IPv4Settings([new IPv4Detail
                {
                    IPAddress = IPAddress.Parse("192.168.1.10"),
                    Netmask = IPAddress.Parse("255.255.255.0")
                }])
                {
                    Gateway = IPAddress.Parse("192.168.1.1")
                }
            }]),
            NetworkDNSSettings = dnsSettings ?? new NetworkDNSSettings { Hostname = "test-host" }
        });
    }

    private static void FireSuccessEvent(DnsControlPanelSaveHandler handler, SetSystemConfiguration command)
    {
        var @event = new SystemConfigurationChanged { CorrelationId = command.CorrelationId };
        _ = handler.Consume(new ClientContext<SystemConfigurationChanged>(@event, command.CorrelationId), CancellationToken.None);
    }

    private void SetupMediatorToFireSuccess(DnsControlPanelSaveHandler handler, Action<SetSystemConfiguration>? onSend = null)
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

    private void SetupMediatorToFireError(DnsControlPanelSaveHandler handler, ErrorInfo error)
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

    private static DnsControlPanelState CreateValidState() => new()
    {
        Hostname = "my-host",
        DnsSuffixEnabled = false,
        DnsSuffix = string.Empty,
        MulticastDnsEnabled = false,
        DnsEnabled = false,
        SearchDomainsEnabled = false,
        StaticHostsEnabled = false,
        DnsDetails = [new NetworkInterfaceDnsDetail { IpAddress = string.Empty }],
        SearchDomainDetails = [new NetworkInterfaceSearchDomainDetail { IpAddress = string.Empty }],
        StaticHostDetails = [new NetworkInterfaceStaticHostDetail { IpAddress = string.Empty, Hostname = string.Empty }]
    };

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
    public async Task Should_save_hostname_from_state()
    {
        // Arrange
        SetupSystemConfiguration();
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.Hostname = "my-custom-host";

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.NetworkDNSSettings.Hostname.Should().Be("my-custom-host");
    }

    [Fact]
    public async Task Should_save_multicast_dns_enabled_from_state()
    {
        // Arrange
        SetupSystemConfiguration();
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.MulticastDnsEnabled = true;

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.NetworkDNSSettings.MulticastDNSEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Should_save_dns_suffix_settings_from_state()
    {
        // Arrange
        SetupSystemConfiguration();
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.DnsSuffixEnabled = true;
        state.DnsSuffix = "example.com";

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.NetworkDNSSettings.DNSSuffixEnabled.Should().BeTrue();
        captured.NetworkDNSSettings.DNSSuffix.Should().Be("example.com");
    }

    [Fact]
    public async Task Should_save_name_servers_from_state()
    {
        // Arrange
        SetupSystemConfiguration();
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.DnsEnabled = true;
        state.DnsDetails =
        [
            new NetworkInterfaceDnsDetail { IpAddress = "8.8.8.8" },
            new NetworkInterfaceDnsDetail { IpAddress = "8.8.4.4" }
        ];

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        var dns = captured!.NetworkDNSSettings;
        dns.NameServersEnabled.Should().BeTrue();
        dns.NameServers.Should().HaveCount(2);
        dns.NameServers[0].Should().Be(IPAddress.Parse("8.8.8.8"));
        dns.NameServers[1].Should().Be(IPAddress.Parse("8.8.4.4"));
    }

    [Fact]
    public async Task Should_filter_out_empty_name_servers()
    {
        // Arrange
        SetupSystemConfiguration();
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.DnsEnabled = true;
        state.DnsDetails =
        [
            new NetworkInterfaceDnsDetail { IpAddress = "8.8.8.8" },
            new NetworkInterfaceDnsDetail { IpAddress = string.Empty }
        ];

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.NetworkDNSSettings.NameServers.Should().HaveCount(1);
        captured.NetworkDNSSettings.NameServers[0].Should().Be(IPAddress.Parse("8.8.8.8"));
    }

    [Fact]
    public async Task Should_save_search_domains_from_state()
    {
        // Arrange
        SetupSystemConfiguration();
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.SearchDomainsEnabled = true;
        state.SearchDomainDetails =
        [
            new NetworkInterfaceSearchDomainDetail { IpAddress = "example.com" },
            new NetworkInterfaceSearchDomainDetail { IpAddress = "local.dev" }
        ];

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        var dns = captured!.NetworkDNSSettings;
        dns.SearchDomainsEnabled.Should().BeTrue();
        dns.SearchDomains.Should().HaveCount(2);
        dns.SearchDomains[0].Should().Be("example.com");
        dns.SearchDomains[1].Should().Be("local.dev");
    }

    [Fact]
    public async Task Should_filter_out_empty_search_domains()
    {
        // Arrange
        SetupSystemConfiguration();
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.SearchDomainsEnabled = true;
        state.SearchDomainDetails =
        [
            new NetworkInterfaceSearchDomainDetail { IpAddress = "example.com" },
            new NetworkInterfaceSearchDomainDetail { IpAddress = string.Empty }
        ];

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.NetworkDNSSettings.SearchDomains.Should().HaveCount(1);
        captured.NetworkDNSSettings.SearchDomains[0].Should().Be("example.com");
    }

    [Fact]
    public async Task Should_save_static_hosts_from_state()
    {
        // Arrange
        SetupSystemConfiguration();
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.StaticHostsEnabled = true;
        state.StaticHostDetails =
        [
            new NetworkInterfaceStaticHostDetail { IpAddress = "192.168.1.100", Hostname = "server1" },
            new NetworkInterfaceStaticHostDetail { IpAddress = "192.168.1.101", Hostname = "server2" }
        ];

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        var dns = captured!.NetworkDNSSettings;
        dns.StaticHostsEnabled.Should().BeTrue();
        dns.StaticHosts.Should().HaveCount(2);
        dns.StaticHosts[0].IPAddress.Should().Be(IPAddress.Parse("192.168.1.100"));
        dns.StaticHosts[0].Hostname.Should().Be("server1");
        dns.StaticHosts[1].IPAddress.Should().Be(IPAddress.Parse("192.168.1.101"));
        dns.StaticHosts[1].Hostname.Should().Be("server2");
    }

    [Fact]
    public async Task Should_filter_out_empty_static_hosts()
    {
        // Arrange
        SetupSystemConfiguration();
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.StaticHostsEnabled = true;
        state.StaticHostDetails =
        [
            new NetworkInterfaceStaticHostDetail { IpAddress = "192.168.1.100", Hostname = "server1" },
            new NetworkInterfaceStaticHostDetail { IpAddress = string.Empty, Hostname = string.Empty }
        ];

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.NetworkDNSSettings.StaticHosts.Should().HaveCount(1);
        captured.NetworkDNSSettings.StaticHosts[0].IPAddress.Should().Be(IPAddress.Parse("192.168.1.100"));
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
        captured!.NetworkInterfacesSettings.Should().Be(_systemConfigurationService.SystemConfiguration.NetworkInterfacesSettings);
    }
}
