using Blazor.Shared.Network.ControlPanels.RemoteAccess.Services;
using Blazor.Shared.Services;
using Core.Shared.HostManagement;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Service;
using HostManagement.Shared.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Tests.Network.ControlPanels.RemoteAccess.Services;

public sealed class RemoteAccessControlPanelResetHandlerTests
{
    private const string SshServiceName = "ssh.service";
    private const string MoneoRcServiceName = "moneo-rc.service";

    private readonly ISystemConfigurationService _systemConfigurationService = Substitute.For<ISystemConfigurationService>();
    private readonly IOptions<HostManagementOptions> _hostMgmtOptions = Options.Create(new HostManagementOptions { SshServiceName = SshServiceName });

    private static IConfiguration CreateConfiguration(string? moneoRcServiceName = MoneoRcServiceName)
    {
        var values = new Dictionary<string, string?>();
        if (moneoRcServiceName is not null)
            values[Constants.MoneoRcServiceNameConfigKey] = moneoRcServiceName;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private ServiceProvider SetupServiceProvider(IConfiguration? config = null)
    {
        var services = new ServiceCollection()
            .AddScoped(_ => _systemConfigurationService)
            .AddScoped(_ => _hostMgmtOptions)
            .AddScoped(_ => config ?? CreateConfiguration())
            .AddScoped<IControlPanelResetHandler<RemoteAccessControlPanelState>, RemoteAccessControlPanelResetHandler>();

        return services.BuildServiceProvider();
    }

    private void SetupSystemConfiguration(params ServiceDetail[] services)
    {
        _systemConfigurationService.SystemConfiguration.Returns(new SystemConfiguration
        {
            Services = [.. services]
        });
    }

    [Fact]
    public async Task Should_set_can_secure_shell_false_when_ssh_service_not_in_list()
    {
        // Arrange
        SetupSystemConfiguration();
        await using var serviceProvider = SetupServiceProvider();
        var state = new RemoteAccessControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<RemoteAccessControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.Terminal.CanSecureShell.Should().BeFalse();
    }

    [Fact]
    public async Task Should_set_is_secure_shell_true_when_ssh_service_is_enabled()
    {
        // Arrange
        SetupSystemConfiguration(new ServiceDetail { Name = SshServiceName, State = ServiceState.Enabled });
        await using var serviceProvider = SetupServiceProvider();
        var state = new RemoteAccessControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<RemoteAccessControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.Terminal.CanSecureShell.Should().BeTrue();
        state.Terminal.IsSecureShell.Should().BeTrue();
        state.IsSecureShellInitial.Should().BeTrue();
        state.IsSecureShellInitial.Should().Be(state.Terminal.IsSecureShell);
    }

    [Fact]
    public async Task Should_set_is_secure_shell_false_when_ssh_service_is_disabled()
    {
        // Arrange
        SetupSystemConfiguration(new ServiceDetail { Name = SshServiceName, State = ServiceState.Disabled });
        await using var serviceProvider = SetupServiceProvider();
        var state = new RemoteAccessControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<RemoteAccessControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.Terminal.IsSecureShell.Should().BeFalse();
    }

    [Fact]
    public async Task Should_set_can_moneo_rc_true_when_moneo_rc_service_exists()
    {
        // Arrange
        SetupSystemConfiguration(new ServiceDetail { Name = MoneoRcServiceName, State = ServiceState.Enabled });
        await using var serviceProvider = SetupServiceProvider();
        var state = new RemoteAccessControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<RemoteAccessControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.Terminal.CanMoneoRc.Should().BeTrue();
    }

    [Fact]
    public async Task Should_set_can_moneo_rc_false_when_moneo_rc_service_not_in_list()
    {
        // Arrange
        SetupSystemConfiguration();
        await using var serviceProvider = SetupServiceProvider();
        var state = new RemoteAccessControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<RemoteAccessControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.Terminal.CanMoneoRc.Should().BeFalse();
    }

    [Fact]
    public async Task Should_set_is_moneo_rc_true_when_moneo_rc_service_is_enabled()
    {
        // Arrange
        SetupSystemConfiguration(new ServiceDetail { Name = MoneoRcServiceName, State = ServiceState.Enabled });
        await using var serviceProvider = SetupServiceProvider();
        var state = new RemoteAccessControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<RemoteAccessControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.Terminal.IsMoneoRc.Should().BeTrue();
        state.IsMoneoRcInitial.Should().BeTrue();
        state.IsMoneoRcInitial.Should().Be(state.Terminal.IsMoneoRc);
    }

    [Fact]
    public async Task Should_set_is_moneo_rc_false_when_moneo_rc_service_is_disabled()
    {
        // Arrange
        SetupSystemConfiguration(new ServiceDetail { Name = MoneoRcServiceName, State = ServiceState.Disabled });
        await using var serviceProvider = SetupServiceProvider();
        var state = new RemoteAccessControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<RemoteAccessControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.Terminal.IsMoneoRc.Should().BeFalse();
    }

    [Fact]
    public async Task Should_set_can_moneo_rc_false_when_moneo_rc_config_key_is_missing()
    {
        // Arrange
        SetupSystemConfiguration(new ServiceDetail { Name = MoneoRcServiceName, State = ServiceState.Enabled });
        await using var serviceProvider = SetupServiceProvider(CreateConfiguration(moneoRcServiceName: null));
        var state = new RemoteAccessControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<RemoteAccessControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.Terminal.CanMoneoRc.Should().BeFalse();
    }
}
