using AwesomeAssertions;
using Blazor.Shared.Network.ControlPanels.Proxies.Models;
using Blazor.Shared.Network.ControlPanels.Proxies.Services;
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

namespace Blazor.Shared.Tests.Network.ControlPanels.Proxies.Services;

public sealed class ProxiesControlPanelSaveHandlerTests
{
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly ISystemConfigurationService _systemConfigurationService = Substitute.For<ISystemConfigurationService>();

    private ProxiesControlPanelSaveHandler CreateHandler() =>
        new(_mediator, _systemConfigurationService, NullLogger<ProxiesControlPanelSaveHandler>.Instance);

    private void SetupSystemConfiguration()
    {
        _systemConfigurationService.SystemConfiguration.Returns(new SystemConfiguration
        {
            NetworkInterfacesSettings = new NetworkInterfacesSettings([new NetworkInterfaceDetail
            {
                CommonInformation = new NetworkInterfaceCommonInformation { Name = "eth0", Enabled = true },
                IPv4 = new IPv4Settings([new IPv4Detail
                {
                    IPAddress = System.Net.IPAddress.Parse("192.168.1.10"),
                    Netmask = System.Net.IPAddress.Parse("255.255.255.0")
                }])
                {
                    Gateway = System.Net.IPAddress.Parse("192.168.1.1")
                }
            }]),
            NetworkDNSSettings = new NetworkDNSSettings { Hostname = "test-host" }
        });
    }

    private static void FireSuccessEvent(ProxiesControlPanelSaveHandler handler, SetSystemConfiguration command)
    {
        var @event = new SystemConfigurationChanged { CorrelationId = command.CorrelationId };
        _ = handler.Consume(new ClientContext<SystemConfigurationChanged>(@event, command.CorrelationId), CancellationToken.None);
    }

    private void SetupMediatorToFireSuccess(ProxiesControlPanelSaveHandler handler, Action<SetSystemConfiguration>? onSend = null)
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

    private void SetupMediatorToFireError(ProxiesControlPanelSaveHandler handler, ErrorInfo error)
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

    private static ProxiesControlPanelState CreateValidState() => new()
    {
        HttpProxySettings = new ProxySettings(),
        HttpsProxySettings = new ProxySettings(),
        SocksProxySettings = new ProxySettings(),
        FtpProxySettings = new ProxySettings(),
        SftpProxySettings = new ProxySettings(),
        DoNotProxyListEnabled = false,
        DoNotProxyDetails = [new DoNotProxyDetail { HostnameOrIp = string.Empty }]
    };

    private static ProxySettings CreateEnabledProxySettings(string server = "http://proxy.example.com", string port = "8080") => new()
    {
        Enabled = true,
        Server = server,
        Port = port
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
    public async Task Should_return_error_result_when_http_proxy_port_is_not_an_integer()
    {
        // Arrange
        SetupSystemConfiguration();
        using var handler = CreateHandler();

        var state = CreateValidState();
        state.HttpProxySettings = new ProxySettings { Enabled = true, Server = "http://proxy.example.com", Port = "not-a-port" };

        // Act
        var result = await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
    }

    [Fact]
    public async Task Should_return_error_result_when_https_proxy_port_is_not_an_integer()
    {
        // Arrange
        SetupSystemConfiguration();
        using var handler = CreateHandler();

        var state = CreateValidState();
        state.HttpsProxySettings = new ProxySettings { Enabled = true, Server = "https://proxy.example.com", Port = "not-a-port" };

        // Act
        var result = await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
    }

    [Fact]
    public async Task Should_return_error_result_when_socks_proxy_port_is_not_an_integer()
    {
        // Arrange
        SetupSystemConfiguration();
        using var handler = CreateHandler();

        var state = CreateValidState();
        state.SocksProxySettings = new ProxySettings { Enabled = true, Server = "socks://proxy.example.com", Port = "not-a-port" };

        // Act
        var result = await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
    }

    [Fact]
    public async Task Should_return_error_result_when_ftp_proxy_port_is_not_an_integer()
    {
        // Arrange
        SetupSystemConfiguration();
        using var handler = CreateHandler();

        var state = CreateValidState();
        state.FtpProxySettings = new ProxySettings { Enabled = true, Server = "ftp://proxy.example.com", Port = "not-a-port" };

        // Act
        var result = await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
    }

    [Fact]
    public async Task Should_return_error_result_when_sftp_proxy_port_is_not_an_integer()
    {
        // Arrange
        SetupSystemConfiguration();
        using var handler = CreateHandler();

        var state = CreateValidState();
        state.SftpProxySettings = new ProxySettings { Enabled = true, Server = "sftp://proxy.example.com", Port = "not-a-port" };

        // Act
        var result = await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
    }

    [Fact]
    public async Task Should_save_http_proxy_settings_from_state()
    {
        // Arrange
        SetupSystemConfiguration();
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.HttpProxySettings = CreateEnabledProxySettings("http://http-proxy.example.com", "3128");

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        var proxy = captured!.NetworkProxySettings.HTTP;
        proxy.Enabled.Should().BeTrue();
        proxy.Server.Should().Be("http://http-proxy.example.com");
        proxy.Port.Should().Be(3128);
    }

    [Fact]
    public async Task Should_save_https_proxy_settings_from_state()
    {
        // Arrange
        SetupSystemConfiguration();
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.HttpsProxySettings = CreateEnabledProxySettings("https://https-proxy.example.com", "3129");

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        var proxy = captured!.NetworkProxySettings.HTTPS;
        proxy.Enabled.Should().BeTrue();
        proxy.Server.Should().Be("https://https-proxy.example.com");
        proxy.Port.Should().Be(3129);
    }

    [Fact]
    public async Task Should_save_socks_proxy_settings_from_state()
    {
        // Arrange
        SetupSystemConfiguration();
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.SocksProxySettings = CreateEnabledProxySettings("socks://socks-proxy.example.com", "1080");

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        var proxy = captured!.NetworkProxySettings.SOCKS;
        proxy.Enabled.Should().BeTrue();
        proxy.Server.Should().Be("socks://socks-proxy.example.com");
        proxy.Port.Should().Be(1080);
    }

    [Fact]
    public async Task Should_save_ftp_proxy_settings_from_state()
    {
        // Arrange
        SetupSystemConfiguration();
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.FtpProxySettings = CreateEnabledProxySettings("ftp://ftp-proxy.example.com", "2121");

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        var proxy = captured!.NetworkProxySettings.FTP;
        proxy.Enabled.Should().BeTrue();
        proxy.Server.Should().Be("ftp://ftp-proxy.example.com");
        proxy.Port.Should().Be(2121);
    }

    [Fact]
    public async Task Should_save_sftp_proxy_settings_from_state()
    {
        // Arrange
        SetupSystemConfiguration();
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.SftpProxySettings = CreateEnabledProxySettings("sftp://sftp-proxy.example.com", "2222");

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        var proxy = captured!.NetworkProxySettings.SFTP;
        proxy.Enabled.Should().BeTrue();
        proxy.Server.Should().Be("sftp://sftp-proxy.example.com");
        proxy.Port.Should().Be(2222);
    }

    [Fact]
    public async Task Should_save_proxy_credentials_when_password_required()
    {
        // Arrange
        SetupSystemConfiguration();
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.HttpProxySettings = new ProxySettings
        {
            Enabled = true,
            Server = "http://proxy.example.com",
            Port = "8080",
            PasswordRequired = true,
            Username = "user",
            Password = "secret"
        };

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        var proxy = captured!.NetworkProxySettings.HTTP;
        proxy.Username.Should().Be("user");
        proxy.Password.Should().Be("secret");
    }

    [Fact]
    public async Task Should_clear_proxy_credentials_when_password_not_required()
    {
        // Arrange
        SetupSystemConfiguration();
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.HttpProxySettings = new ProxySettings
        {
            Enabled = true,
            Server = "http://proxy.example.com",
            Port = "8080",
            PasswordRequired = false,
            Username = "user",
            Password = "secret"
        };

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        var proxy = captured!.NetworkProxySettings.HTTP;
        proxy.Username.Should().BeNull();
        proxy.Password.Should().BeNull();
    }

    [Fact]
    public async Task Should_save_do_not_proxy_list_from_state()
    {
        // Arrange
        SetupSystemConfiguration();
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.DoNotProxyListEnabled = true;
        state.DoNotProxyDetails =
        [
            new DoNotProxyDetail { HostnameOrIp = "192.168.1.50" },
            new DoNotProxyDetail { HostnameOrIp = "internal.example.com" }
        ];

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        var proxy = captured!.NetworkProxySettings;
        proxy.DoNotProxyListEnabled.Should().BeTrue();
        proxy.DoNotProxyList.Should().HaveCount(2);
        proxy.DoNotProxyList[0].Should().Be("192.168.1.50");
        proxy.DoNotProxyList[1].Should().Be("internal.example.com");
    }

    [Fact]
    public async Task Should_filter_out_empty_do_not_proxy_entries()
    {
        // Arrange
        SetupSystemConfiguration();
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.DoNotProxyListEnabled = true;
        state.DoNotProxyDetails =
        [
            new DoNotProxyDetail { HostnameOrIp = "192.168.1.50" },
            new DoNotProxyDetail { HostnameOrIp = string.Empty }
        ];

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.NetworkProxySettings.DoNotProxyList.Should().HaveCount(1);
        captured.NetworkProxySettings.DoNotProxyList[0].Should().Be("192.168.1.50");
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
