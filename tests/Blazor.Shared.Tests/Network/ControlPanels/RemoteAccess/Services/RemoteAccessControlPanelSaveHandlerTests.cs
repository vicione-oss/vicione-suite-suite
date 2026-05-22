using AwesomeAssertions;
using Blazor.Shared.Network.ControlPanels.RemoteAccess.Services;
using Blazor.Shared.Services;
using Core.Shared.HostManagement;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Events;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;
using HostManagement.Shared.Contracts.Service;
using HostManagement.Shared.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Sdk.SystemConfiguration.Events;
using Xunit;

namespace Blazor.Shared.Tests.Network.ControlPanels.RemoteAccess.Services;

public sealed class RemoteAccessControlPanelSaveHandlerTests
{
    private const string SshServiceName = "ssh.service";
    private const string MoneoRcServiceName = "moneo-rc.service";

    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly ISystemConfigurationService _systemConfigurationService = Substitute.For<ISystemConfigurationService>();
    private readonly IOptions<HostManagementOptions> _hostMgmtOptions = Options.Create(new HostManagementOptions { SshServiceName = SshServiceName });

    private static IConfiguration CreateConfiguration(string? moneoRcServiceName = MoneoRcServiceName)
    {
        var values = new Dictionary<string, string?>();
        if (moneoRcServiceName is not null)
            values[Constants.MoneoRcServiceNameConfigKey] = moneoRcServiceName;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private RemoteAccessControlPanelSaveHandler CreateHandler(IConfiguration? config = null) =>
        new(_mediator, _systemConfigurationService, NullLogger<RemoteAccessControlPanelSaveHandler>.Instance,
            _hostMgmtOptions, config ?? CreateConfiguration());

    private void SetupSystemConfiguration(params ServiceDetail[] services)
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
            NetworkDNSSettings = new NetworkDNSSettings { Hostname = "test-host" },
            Services = [.. services]
        });
    }

    private static void FireSuccessEvent(RemoteAccessControlPanelSaveHandler handler, SetSystemConfiguration command)
    {
        var @event = new SystemConfigurationChanged { CorrelationId = command.CorrelationId };
        _ = handler.Consume(new ClientContext<SystemConfigurationChanged>(@event, command.CorrelationId), CancellationToken.None);
    }

    private void SetupMediatorToFireSuccess(RemoteAccessControlPanelSaveHandler handler, Action<SetSystemConfiguration>? onSend = null)
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

    private void SetupMediatorToFireError(RemoteAccessControlPanelSaveHandler handler, ErrorInfo error)
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

    private static RemoteAccessControlPanelState CreateValidState() => new();

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
        SetupMediatorToFireError(handler, new ErrorInfo(42, "Something went wrong"));

        var state = CreateValidState();

        // Act
        var result = await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
        ((SaveErrorResult)result).Message.Should().Be("Something went wrong");
    }

    [Fact]
    public async Task Should_enable_ssh_service_when_can_and_is_secure_shell()
    {
        // Arrange
        SetupSystemConfiguration(new ServiceDetail { Name = SshServiceName, State = ServiceState.Disabled });
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.Terminal.CanSecureShell = true;
        state.Terminal.IsSecureShell = true;

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.Services.Single(s => s.Name == SshServiceName).State.Should().Be(ServiceState.Enabled);
    }

    [Fact]
    public async Task Should_disable_ssh_service_when_can_but_is_not_secure_shell()
    {
        // Arrange
        SetupSystemConfiguration(new ServiceDetail { Name = SshServiceName, State = ServiceState.Enabled });
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.Terminal.CanSecureShell = true;
        state.Terminal.IsSecureShell = false;

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.Services.Single(s => s.Name == SshServiceName).State.Should().Be(ServiceState.Disabled);
    }

    [Fact]
    public async Task Should_disable_ssh_service_when_cannot_secure_shell()
    {
        // Arrange
        SetupSystemConfiguration(new ServiceDetail { Name = SshServiceName, State = ServiceState.Enabled });
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.Terminal.CanSecureShell = false;
        state.Terminal.IsSecureShell = true;

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.Services.Single(s => s.Name == SshServiceName).State.Should().Be(ServiceState.Disabled);
    }

    [Fact]
    public async Task Should_not_modify_ssh_service_when_not_in_services_list()
    {
        // Arrange
        SetupSystemConfiguration(); // no services
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.Terminal.CanSecureShell = true;
        state.Terminal.IsSecureShell = true;

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.Services.Should().NotContain(s => s.Name == SshServiceName);
    }

    [Fact]
    public async Task Should_enable_moneo_rc_service_when_can_and_is_moneo_rc()
    {
        // Arrange
        SetupSystemConfiguration(new ServiceDetail { Name = MoneoRcServiceName, State = ServiceState.Disabled });
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.Terminal.CanMoneoRc = true;
        state.Terminal.IsMoneoRc = true;

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.Services.Single(s => s.Name == MoneoRcServiceName).State.Should().Be(ServiceState.Enabled);
    }

    [Fact]
    public async Task Should_disable_moneo_rc_service_when_can_but_is_not_moneo_rc()
    {
        // Arrange
        SetupSystemConfiguration(new ServiceDetail { Name = MoneoRcServiceName, State = ServiceState.Enabled });
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.Terminal.CanMoneoRc = true;
        state.Terminal.IsMoneoRc = false;

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.Services.Single(s => s.Name == MoneoRcServiceName).State.Should().Be(ServiceState.Disabled);
    }

    [Fact]
    public async Task Should_disable_moneo_rc_service_when_cannot_moneo_rc()
    {
        // Arrange
        SetupSystemConfiguration(new ServiceDetail { Name = MoneoRcServiceName, State = ServiceState.Enabled });
        SystemConfiguration? captured = null;
        using var handler = CreateHandler();
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.Terminal.CanMoneoRc = false;
        state.Terminal.IsMoneoRc = true;

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.Services.Single(s => s.Name == MoneoRcServiceName).State.Should().Be(ServiceState.Disabled);
    }

    [Fact]
    public async Task Should_not_modify_moneo_rc_service_when_config_key_is_missing()
    {
        // Arrange
        SetupSystemConfiguration(new ServiceDetail { Name = MoneoRcServiceName, State = ServiceState.Enabled });
        SystemConfiguration? captured = null;
        using var handler = CreateHandler(CreateConfiguration(moneoRcServiceName: null));
        SetupMediatorToFireSuccess(handler, cmd => captured = cmd.SystemConfiguration);

        var state = CreateValidState();
        state.Terminal.CanMoneoRc = true;
        state.Terminal.IsMoneoRc = false;

        // Act
        await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        captured!.Services.Single(s => s.Name == MoneoRcServiceName).State.Should().Be(ServiceState.Enabled);
    }
}
