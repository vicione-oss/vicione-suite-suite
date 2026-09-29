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
    private const int TcpIpPageIndex = 1;

    private static ISystemConfigurationService ConfigureServices(
        BunitContext ctx,
        IUiMediator mediator,
        bool selectActivePageManually = false)
    {
        var timeProvider = Substitute.For<TimeProvider>();
        var systemConfigurationService = Substitute.For<ISystemConfigurationService>();
        timeProvider.LocalTimeZone.Returns(TimeZoneInfo.Utc);

        systemConfigurationService.SystemConfiguration.Returns(new SystemConfiguration());

        ctx.SetupBlazorSharedSettings(setup =>
        {
            setup.Services.AddSingleton(systemConfigurationService);
            setup.Services.AddKeyedScoped(Sdk.Constants.ClientTimeProviderServiceKey, (_, __) => timeProvider);

            setup.Services.AddControlPanelInfrastructure();
            setup.Services.AddNetwork();
            setup.Services.AddSingleton(mediator);

            // RenderControlPanelPage drives which page is active, which needs a substitute to steer
            if (selectActivePageManually)
                setup.Services.AddSingleton(Substitute.For<IActiveControlPanelPageProvider>());
        });

        return systemConfigurationService;
    }

    private static IEnumerable<IRenderedComponent<SettingsField>> FindLoadingSettingsFields(IRenderedComponent<NetworkInterfaceControlPanel> component)
        => component.FindComponents<SettingsField>().Where(field => field.Instance.IsLoading);

    private static IRenderedComponent<SettingsFieldButton> FindSettingsFieldButton(IRenderedComponent<NetworkInterfaceControlPanel> component, string text)
        => component.FindComponents<SettingsFieldButton>().Single(button => button.Instance.Text == text);

    private static IRenderedComponent<NetworkInterfaceControlPanel> RenderControlPanel(
        BunitContext ctx,
        IUiMediator mediator,
        NetworkInterfaceControlPanelState controlPanelState)
    {
        var systemConfigurationService = ConfigureServices(ctx, mediator);

        var registry = ctx.Services.GetRequiredService<IControlPanelRegistry<SharedClientModule>>();

        var registryItem = registry.Add<NetworkInterfaceControlPanel, NetworkInterfaceControlPanelState>(
            new NetworkInterfaceControlPanelDescriptor(controlPanelState, systemConfigurationService),
            controlPanelState,
            new ControlPanelNetworkCategoryDescriptor());

        return ctx.Render<NetworkInterfaceControlPanel>(builder => builder
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

        var systemConfigurationService = ConfigureServices(ctx, mediator, selectActivePageManually: true);

        var component = ctx.RenderControlPanelPage<NetworkInterfaceControlPanel, NetworkInterfaceControlPanelState>(controlPanelState, TcpIpPageIndex);

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
}
