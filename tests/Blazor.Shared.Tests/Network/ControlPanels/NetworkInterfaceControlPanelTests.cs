using System.Net;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Components;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;
using Blazor.Shared.Network.Extensions;
using Blazor.Shared.Network.Services;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using Bunit;
using Core.Shared.HostManagement.Requests;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Components.Settings;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.NetworkStatus.Requests;
using ViciOne.Ui.Blazor.Components.Button.Enums;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Tests.Network.ControlPanels;

public sealed class NetworkInterfaceControlPanelTests
{
    private static ISystemConfigurationService ConfigureServices(
        BunitContext ctx,
        IUiMediator mediator,
        SystemConfiguration? systemConfiguration = null)
    {
        var timeProvider = Substitute.For<TimeProvider>();
        var systemConfigurationService = Substitute.For<ISystemConfigurationService>();
        timeProvider.LocalTimeZone.Returns(TimeZoneInfo.Utc);

        systemConfigurationService.SystemConfiguration.Returns(systemConfiguration ?? new SystemConfiguration());

        ctx.SetupBlazorSharedSettings(setup =>
        {
            setup.Services.AddSingleton(systemConfigurationService);
            setup.Services.AddKeyedScoped(Sdk.Constants.ClientTimeProviderServiceKey, (_, __) => timeProvider);

            setup.Services.AddControlPanelInfrastructure();
            setup.Services.AddNetwork();
            setup.Services.AddSingleton(mediator);
        });

        return systemConfigurationService;
    }

    private static IEnumerable<IRenderedComponent<SettingsField>> FindLoadingSettingsFields(IRenderedComponent<IComponent> component)
        => component.FindComponents<SettingsField>().Where(field => field.Instance.IsLoading);

    private static IRenderedComponent<SettingsFieldButton> FindSettingsFieldButton(IRenderedComponent<IComponent> component, string text)
        => component.FindComponents<SettingsFieldButton>().Single(button => button.Instance.Text == text);

    private static IRenderedComponent<NetworkInterfaceGeneralControlPanel> RenderControlPanel(
        BunitContext ctx,
        IUiMediator mediator,
        NetworkInterfaceControlPanelState controlPanelState,
        SystemConfiguration? systemConfiguration = null)
    {
        var systemConfigurationService = ConfigureServices(ctx, mediator, systemConfiguration);

        var registry = ctx.Services.GetRequiredService<IControlPanelRegistry<SharedClientModule>>();

        var registryItem = registry.Add<NetworkInterfaceGeneralControlPanel, NetworkInterfaceControlPanelState>(
            new NetworkInterfaceControlPanelDescriptor(controlPanelState, systemConfigurationService),
            controlPanelState,
            new ControlPanelNetworkCategoryDescriptor());

        return ctx.Render<NetworkInterfaceGeneralControlPanel>(builder => builder
            .Add(p => p.State, controlPanelState)
            .AddCascadingValue(registryItem));
    }

    [Fact]
    public async Task Should_render_component()
    {
        // Arrange
        var controlPanelState = new NetworkInterfaceControlPanelState();
        var mediator = Substitute.For<IUiMediator>();

        mediator.Request<GetDHCPLeaseInformation, GetDHCPLeaseInformationResponse>(Arg.Any<GetDHCPLeaseInformation>(), Arg.Any<CancellationToken>())
            .Returns(new GetDHCPLeaseInformationResponse());

        await using var ctx = new BunitContext();

        // Act
        var component = RenderControlPanel(ctx, mediator, controlPanelState);

        // Assert
        component.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_show_the_test_connection_button_as_busy_while_the_test_runs()
    {
        // Arrange
        var controlPanelState = new NetworkInterfaceControlPanelState { Enabled = true };
        var mediator = Substitute.For<IUiMediator>();

        mediator.Request<GetDHCPLeaseInformation, GetDHCPLeaseInformationResponse>(Arg.Any<GetDHCPLeaseInformation>(), Arg.Any<CancellationToken>())
            .Returns(new GetDHCPLeaseInformationResponse());

        TaskCompletionSource<GetNetworkStatusInformationResponse> testRunning = new();

        mediator.Request<GetNetworkStatusInformation, GetNetworkStatusInformationResponse>(Arg.Any<GetNetworkStatusInformation>(), Arg.Any<CancellationToken>())
            .Returns(testRunning.Task);

        await using var ctx = new BunitContext();

        var component = RenderControlPanel(ctx, mediator, controlPanelState);

        // Act
        var click = FindSettingsFieldButton(component, CommonVocabulary.TestVerb).Find("button").ClickAsync();

        // Assert
        component.WaitForAssertion(() =>
        {
            var testButton = FindSettingsFieldButton(component, CommonVocabulary.TestVerb).Instance;

            testButton.Busy.Should().BeTrue();
            testButton.BusyIndication.Should().Be(ButtonBusyIndication.SweepAndSpinningIcon);
        });

        testRunning.SetResult(new GetNetworkStatusInformationResponse());

        await click;

        component.WaitForAssertion(() => FindSettingsFieldButton(component, CommonVocabulary.TestVerb).Instance.Busy.Should().BeFalse());
    }

    [Fact]
    public async Task Should_keep_the_lease_fields_dimmed_when_the_configuration_changes_during_a_renew()
    {
        // Arrange
        var controlPanelState = new NetworkInterfaceControlPanelState { IpV4ConfigurationMode = IpConfigurationMode.AutomaticDhcp };
        var mediator = Substitute.For<IUiMediator>();

        mediator.Request<GetDHCPLeaseInformation, GetDHCPLeaseInformationResponse>(Arg.Any<GetDHCPLeaseInformation>(), Arg.Any<CancellationToken>())
            .Returns(new GetDHCPLeaseInformationResponse());

        TaskCompletionSource<RenewDHCPLeaseResponse> renewing = new();

        mediator.Request<RenewDHCPLease, RenewDHCPLeaseResponse>(Arg.Any<RenewDHCPLease>(), Arg.Any<CancellationToken>())
            .Returns(renewing.Task);

        await using var ctx = new BunitContext();

        var systemConfigurationService = ConfigureServices(ctx, mediator);

        var component = ctx.Render<NetworkInterfaceTcpIpControlPanel>(builder => builder.Add(p => p.State, controlPanelState));

        var click = FindSettingsFieldButton(component, CommonVocabulary.Renew).Find("button").ClickAsync();

        component.WaitForAssertion(() => FindLoadingSettingsFields(component).Should().NotBeEmpty());

        // Act
        systemConfigurationService.SystemConfigurationChanged += Raise.Event<Func<CancellationToken, Task>>(Xunit.TestContext.Current.CancellationToken);

        // Assert
        FindLoadingSettingsFields(component).Should().NotBeEmpty();
        FindSettingsFieldButton(component, CommonVocabulary.Renew).Instance.Busy.Should().BeTrue();

        renewing.SetResult(new RenewDHCPLeaseResponse());

        await click;
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task Should_disable_the_lease_renewal_while_any_tab_has_unsaved_changes(bool generalHasChanges, bool tcpIpHasChanges, bool hardwareHasChanges)
    {
        // Arrange
        var controlPanelState = new NetworkInterfaceControlPanelState
        {
            IpV4ConfigurationMode = IpConfigurationMode.AutomaticDhcp,
            GeneralHasChanges = generalHasChanges,
            TcpIpHasChanges = tcpIpHasChanges,
            HardwareHasChanges = hardwareHasChanges
        };
        var mediator = Substitute.For<IUiMediator>();

        await using var ctx = new BunitContext();

        ConfigureServices(ctx, mediator);

        // Act
        var component = ctx.Render<NetworkInterfaceTcpIpControlPanel>(builder => builder.Add(p => p.State, controlPanelState));

        // Assert
        FindSettingsFieldButton(component, CommonVocabulary.Renew).Instance.Enabled.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Should_show_the_static_dns_servers_only_while_they_are_enabled(bool nameServersEnabled)
    {
        // Arrange
        var systemConfiguration = CreateSystemConfiguration(dhcpEnabled: true, nameServersEnabled: nameServersEnabled);
        var controlPanelState = new NetworkInterfaceControlPanelState
        {
            DHCPLease = CreateLease("10.0.0.5") with
            {
                NetworkDNSSettings = new NetworkDNSSettings { NameServers = new() { Addresses = [IPAddress.Parse("1.1.1.1")] } }
            }
        };

        await using var ctx = new BunitContext();

        // Act
        var component = RenderControlPanel(ctx, Substitute.For<IUiMediator>(), controlPanelState, systemConfiguration);

        // Assert
        var dnsServers = FindDnsServers(component);

        dnsServers.Contains("8.8.8.8", StringComparison.Ordinal).Should().Be(nameServersEnabled);
        dnsServers.Should().Contain("1.1.1.1");
    }

    [Fact]
    public async Task Should_show_disabled_when_the_static_dns_servers_are_disabled_and_dhcp_provides_none()
    {
        // Arrange
        var systemConfiguration = CreateSystemConfiguration(dhcpEnabled: false, nameServersEnabled: false);

        await using var ctx = new BunitContext();

        // Act
        var component = RenderControlPanel(ctx, Substitute.For<IUiMediator>(), new NetworkInterfaceControlPanelState(), systemConfiguration);

        // Assert
        FindDnsServers(component).Trim().Should().Be(TechnicalTerms.Disabled);
    }

    [Fact]
    public async Task Should_show_the_leased_address_when_the_configuration_changes_to_dhcp_outside_the_dispatcher()
    {
        // Arrange
        var mediator = CreateMediatorReturningLease("10.0.0.5");

        await using var ctx = new BunitContext();

        var component = RenderControlPanel(ctx, mediator, new NetworkInterfaceControlPanelState(),
            CreateSystemConfiguration(dhcpEnabled: false, nameServersEnabled: true));
        var systemConfigurationService = ctx.Services.GetRequiredService<ISystemConfigurationService>();

        component.Markup.Should().Contain("192.168.1.10");

        systemConfigurationService.SystemConfiguration.Returns(CreateSystemConfiguration(dhcpEnabled: true, nameServersEnabled: true));

        // Act
        await Task.Run(() => systemConfigurationService.SystemConfigurationChanged +=
            Raise.Event<Func<CancellationToken, Task>>(Xunit.TestContext.Current.CancellationToken), Xunit.TestContext.Current.CancellationToken);

        // Assert
        component.WaitForAssertion(() =>
        {
            component.Markup.Should().Contain("10.0.0.5");
            component.Markup.Should().NotContain("192.168.1.10");
        });
    }

    [Fact]
    public async Task Should_fetch_the_lease_when_mounted_with_dhcp_enabled_and_no_lease_known()
    {
        // Arrange
        var mediator = CreateMediatorReturningLease("10.0.0.5");

        await using var ctx = new BunitContext();

        // Act
        var component = RenderControlPanel(ctx, mediator, new NetworkInterfaceControlPanelState(),
            CreateSystemConfiguration(dhcpEnabled: true, nameServersEnabled: true));

        // Assert
        component.WaitForAssertion(() => component.Markup.Should().Contain("10.0.0.5"));
    }

    [Fact]
    public async Task Should_fetch_the_lease_on_the_tcp_ip_tab_when_the_configuration_changes_to_dhcp()
    {
        // Arrange
        var mediator = CreateMediatorReturningLease("10.0.0.5");
        var controlPanelState = new NetworkInterfaceControlPanelState();

        await using var ctx = new BunitContext();

        var systemConfigurationService = ConfigureServices(ctx, mediator, CreateSystemConfiguration(dhcpEnabled: false, nameServersEnabled: true));

        ctx.Render<NetworkInterfaceTcpIpControlPanel>(builder => builder.Add(p => p.State, controlPanelState));

        systemConfigurationService.SystemConfiguration.Returns(CreateSystemConfiguration(dhcpEnabled: true, nameServersEnabled: true));

        // Act
        await Task.Run(() => systemConfigurationService.SystemConfigurationChanged +=
            Raise.Event<Func<CancellationToken, Task>>(Xunit.TestContext.Current.CancellationToken), Xunit.TestContext.Current.CancellationToken);

        // Assert
        await mediator.Received(1).Request<GetDHCPLeaseInformation, GetDHCPLeaseInformationResponse>(
            Arg.Any<GetDHCPLeaseInformation>(), Arg.Any<CancellationToken>());
        controlPanelState.DHCPLease!.IPv4Detail!.IPAddress.Should().Be(IPAddress.Parse("10.0.0.5"));
    }

    [Fact]
    public async Task Should_rerender_the_hardware_tab_when_the_configuration_changes_outside_the_dispatcher()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();

        await using var ctx = new BunitContext();

        var systemConfigurationService = ConfigureServices(ctx, mediator);

        var component = ctx.Render<NetworkInterfaceHardwareControlPanel>(builder => builder.Add(p => p.State, new NetworkInterfaceControlPanelState()));
        var renderCount = component.RenderCount;

        // Act
        await Task.Run(() => systemConfigurationService.SystemConfigurationChanged +=
            Raise.Event<Func<CancellationToken, Task>>(Xunit.TestContext.Current.CancellationToken), Xunit.TestContext.Current.CancellationToken);

        // Assert
        component.WaitForAssertion(() => component.RenderCount.Should().BeGreaterThan(renderCount));
    }

    private static SystemConfiguration CreateSystemConfiguration(bool dhcpEnabled, bool nameServersEnabled)
        => new()
        {
            NetworkInterfaces =
            [
                new NetworkInterfaceDetail
                {
                    CommonInformation = new NetworkInterfaceCommonInformation { Name = "eth0", Enabled = true },
                    IPv4 = new IPv4Settings
                    {
                        DHCPEnabled = dhcpEnabled,
                        IPv4Details = [new IPv4Detail { IPAddress = IPAddress.Parse("192.168.1.10"), Netmask = IPAddress.Parse("255.255.255.0") }]
                    }
                }
            ],
            NetworkDNSSettings = new NetworkDNSSettings
            {
                NameServers = new() { Enabled = nameServersEnabled, Addresses = [IPAddress.Parse("8.8.8.8")] }
            }
        };

    private static DHCPLease CreateLease(string ipAddress)
        => new() { IPv4Detail = new IPv4Detail { IPAddress = IPAddress.Parse(ipAddress), Netmask = IPAddress.Parse("255.255.255.0") } };

    private static IUiMediator CreateMediatorReturningLease(string ipAddress)
    {
        var mediator = Substitute.For<IUiMediator>();

        mediator.Request<GetDHCPLeaseInformation, GetDHCPLeaseInformationResponse>(Arg.Any<GetDHCPLeaseInformation>(), Arg.Any<CancellationToken>())
            .Returns(new GetDHCPLeaseInformationResponse { DHCPLease = CreateLease(ipAddress) });

        return mediator;
    }

    private static string FindDnsServers(IRenderedComponent<IComponent> component)
        => component.FindComponents<SettingsField>()
            .Single(field => field.Instance.Label == TechnicalTerms.DnsServer)
            .Find(".dns-server-grid")
            .TextContent;
}
