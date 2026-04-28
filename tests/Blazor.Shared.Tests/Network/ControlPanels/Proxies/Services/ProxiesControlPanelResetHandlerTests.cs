using AwesomeAssertions;
using Blazor.Shared.Network.ControlPanels.Proxies.Services;
using Core.Shared.HostManagement.Services;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Services;
using Xunit;

namespace Blazor.Shared.Tests.Network.ControlPanels.Proxies.Services;

public sealed class ProxiesControlPanelResetHandlerTests
{
    private readonly ISystemConfigurationService _systemConfigurationService = Substitute.For<ISystemConfigurationService>();

    private ServiceProvider SetupServiceProvider()
    {
        var services = new ServiceCollection()
            .AddScoped(_ => _systemConfigurationService)
            .AddScoped<IControlPanelResetHandler<ProxiesControlPanelState>, ProxiesControlPanelResetHandler>();

        return services.BuildServiceProvider();
    }

    private void SetupSystemConfiguration(NetworkProxySettings proxySettings)
    {
        _systemConfigurationService.SystemConfiguration.Returns(new SystemConfiguration
        {
            NetworkProxySettings = proxySettings
        });
    }

    [Fact]
    public async Task Should_end_loading_after_reset()
    {
        // Arrange
        SetupSystemConfiguration(new NetworkProxySettings());
        await using var serviceProvider = SetupServiceProvider();

        var state = new ProxiesControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<ProxiesControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.IsLoading.Should().BeFalse();
    }

    [Theory]
    [InlineData("http")]
    [InlineData("https")]
    [InlineData("socks")]
    [InlineData("ftp")]
    [InlineData("sftp")]
    public async Task Should_load_proxy_settings_from_system_configuration(string protocol)
    {
        // Arrange
        var proxyDetail = new NetworkProxyDetail { Enabled = true, Server = "proxy.example.com", Port = 8080, Username = "user", Password = "pass" };

        var networkProxySettings = new NetworkProxySettings();
        switch (protocol)
        {
            case "http": networkProxySettings.HTTP = proxyDetail; break;
            case "https": networkProxySettings.HTTPS = proxyDetail; break;
            case "socks": networkProxySettings.SOCKS = proxyDetail; break;
            case "ftp": networkProxySettings.FTP = proxyDetail; break;
            case "sftp": networkProxySettings.SFTP = proxyDetail; break;
        }

        SetupSystemConfiguration(networkProxySettings);
        await using var serviceProvider = SetupServiceProvider();

        var state = new ProxiesControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<ProxiesControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        var proxySettings = protocol switch
        {
            "http" => state.HttpProxySettings,
            "https" => state.HttpsProxySettings,
            "socks" => state.SocksProxySettings,
            "ftp" => state.FtpProxySettings,
            "sftp" => state.SftpProxySettings,
            _ => throw new ArgumentOutOfRangeException(nameof(protocol))
        };

        proxySettings.Enabled.Should().BeTrue();
        proxySettings.Server.Should().Be("proxy.example.com");
        proxySettings.Port.Should().Be("8080");
        proxySettings.Username.Should().Be("user");
        proxySettings.Password.Should().Be("pass");
    }

    [Fact]
    public async Task Should_set_password_required_when_username_is_set()
    {
        // Arrange
        SetupSystemConfiguration(new NetworkProxySettings { HTTP = new NetworkProxyDetail { Username = "user" } });
        await using var serviceProvider = SetupServiceProvider();

        var state = new ProxiesControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<ProxiesControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.HttpProxySettings.PasswordRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Should_set_password_required_when_password_is_set()
    {
        // Arrange
        SetupSystemConfiguration(new NetworkProxySettings { HTTP = new NetworkProxyDetail { Password = "secret" } });
        await using var serviceProvider = SetupServiceProvider();

        var state = new ProxiesControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<ProxiesControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.HttpProxySettings.PasswordRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Should_not_set_password_required_when_no_credentials_are_set()
    {
        // Arrange
        SetupSystemConfiguration(new NetworkProxySettings { HTTP = new NetworkProxyDetail { Server = "proxy.example.com" } });
        await using var serviceProvider = SetupServiceProvider();

        var state = new ProxiesControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<ProxiesControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.HttpProxySettings.PasswordRequired.Should().BeFalse();
    }

    [Fact]
    public async Task Should_load_do_not_proxy_list_settings_from_system_configuration()
    {
        // Arrange
        SetupSystemConfiguration(new NetworkProxySettings(doNotProxyList: ["192.168.1.0/24", "localhost"])
        {
            DoNotProxyListEnabled = true
        });
        await using var serviceProvider = SetupServiceProvider();

        var state = new ProxiesControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<ProxiesControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.DoNotProxyListEnabled.Should().BeTrue();
        state.DoNotProxyDetails.Should().HaveCount(2);
        state.DoNotProxyDetails[0].HostnameOrIp.Should().Be("192.168.1.0/24");
        state.DoNotProxyDetails[1].HostnameOrIp.Should().Be("localhost");
    }

    [Fact]
    public async Task Should_have_one_empty_do_not_proxy_detail_when_list_is_empty()
    {
        // Arrange
        SetupSystemConfiguration(new NetworkProxySettings());
        await using var serviceProvider = SetupServiceProvider();

        var state = new ProxiesControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<ProxiesControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.DoNotProxyDetails.Should().HaveCount(1);
        state.DoNotProxyDetails[0].HostnameOrIp.Should().BeEmpty();
    }
}
