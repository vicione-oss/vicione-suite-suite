using AwesomeAssertions;
using Blazor.Shared.Network.ControlPanels.Dns.Services;
using Blazor.Shared.Services;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Services;
using Xunit;

namespace Blazor.Shared.Tests.Network.ControlPanels.Dns.Services;

public sealed class DnsControlPanelResetHandlerTests
{
    private readonly ISystemConfigurationService _systemConfigurationService = Substitute.For<ISystemConfigurationService>();

    private ServiceProvider SetupServiceProvider()
    {
        var services = new ServiceCollection()
            .AddScoped(_ => _systemConfigurationService)
            .AddScoped<IControlPanelResetHandler<DnsControlPanelState>, DnsControlPanelResetHandler>();

        return services.BuildServiceProvider();
    }

    private void SetupSystemConfiguration(NetworkDNSSettings dnsSettings)
    {
        _systemConfigurationService.SystemConfiguration.Returns(new SystemConfiguration
        {
            NetworkDNSSettings = dnsSettings
        });
    }

    [Fact]
    public async Task Should_end_loading_after_reset()
    {
        // Arrange
        SetupSystemConfiguration(new NetworkDNSSettings());
        await using var serviceProvider = SetupServiceProvider();

        var state = new DnsControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<DnsControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task Should_load_basic_dns_settings_from_system_configuration()
    {
        // Arrange
        SetupSystemConfiguration(new NetworkDNSSettings
        {
            Hostname = "myhost",
            DNSSuffixEnabled = true,
            DNSSuffix = "example.com",
            MulticastDNSEnabled = true,
            NameServersEnabled = true,
            SearchDomainsEnabled = true,
            StaticHostsEnabled = true
        });
        await using var serviceProvider = SetupServiceProvider();

        var state = new DnsControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<DnsControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.Hostname.Should().Be("myhost");
        state.DnsSuffixEnabled.Should().BeTrue();
        state.DnsSuffix.Should().Be("example.com");
        state.MulticastDnsEnabled.Should().BeTrue();
        state.DnsEnabled.Should().BeTrue();
        state.SearchDomainsEnabled.Should().BeTrue();
        state.StaticHostsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Should_load_dns_server_details_from_system_configuration()
    {
        // Arrange
        SetupSystemConfiguration(new NetworkDNSSettings(nameServers:
        [
            System.Net.IPAddress.Parse("8.8.8.8"),
            System.Net.IPAddress.Parse("1.1.1.1")
        ]));
        await using var serviceProvider = SetupServiceProvider();

        var state = new DnsControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<DnsControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.DnsDetails.Should().HaveCount(2);
        state.DnsDetails[0].IpAddress.Should().Be("8.8.8.8");
        state.DnsDetails[1].IpAddress.Should().Be("1.1.1.1");
    }

    [Fact]
    public async Task Should_have_one_empty_dns_detail_when_no_name_servers_configured()
    {
        // Arrange
        SetupSystemConfiguration(new NetworkDNSSettings());
        await using var serviceProvider = SetupServiceProvider();

        var state = new DnsControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<DnsControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.DnsDetails.Should().HaveCount(1);
        state.DnsDetails[0].IpAddress.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_load_search_domain_details_from_system_configuration()
    {
        // Arrange
        SetupSystemConfiguration(new NetworkDNSSettings(searchDomains: ["corp.example.com", "dev.example.com"]));
        await using var serviceProvider = SetupServiceProvider();

        var state = new DnsControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<DnsControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.SearchDomainDetails.Should().HaveCount(2);
        state.SearchDomainDetails[0].IpAddress.Should().Be("corp.example.com");
        state.SearchDomainDetails[1].IpAddress.Should().Be("dev.example.com");
    }

    [Fact]
    public async Task Should_have_one_empty_search_domain_detail_when_no_search_domains_configured()
    {
        // Arrange
        SetupSystemConfiguration(new NetworkDNSSettings());
        await using var serviceProvider = SetupServiceProvider();

        var state = new DnsControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<DnsControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.SearchDomainDetails.Should().HaveCount(1);
        state.SearchDomainDetails[0].IpAddress.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_load_static_host_details_from_system_configuration()
    {
        // Arrange
        SetupSystemConfiguration(new NetworkDNSSettings(staticHosts:
        [
            new StaticHostDetail { IPAddress = System.Net.IPAddress.Parse("192.168.1.100"), Hostname = "server.local" },
            new StaticHostDetail { IPAddress = System.Net.IPAddress.Parse("10.0.0.5"), Hostname = "printer.local" }
        ]));
        await using var serviceProvider = SetupServiceProvider();

        var state = new DnsControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<DnsControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.StaticHostDetails.Should().HaveCount(2);
        state.StaticHostDetails[0].IpAddress.Should().Be("192.168.1.100");
        state.StaticHostDetails[0].Hostname.Should().Be("server.local");
        state.StaticHostDetails[1].IpAddress.Should().Be("10.0.0.5");
        state.StaticHostDetails[1].Hostname.Should().Be("printer.local");
    }

    [Fact]
    public async Task Should_have_one_empty_static_host_detail_when_no_static_hosts_configured()
    {
        // Arrange
        SetupSystemConfiguration(new NetworkDNSSettings());
        await using var serviceProvider = SetupServiceProvider();

        var state = new DnsControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<DnsControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.StaticHostDetails.Should().HaveCount(1);
        state.StaticHostDetails[0].IpAddress.Should().BeEmpty();
        state.StaticHostDetails[0].Hostname.Should().BeEmpty();
    }
}
