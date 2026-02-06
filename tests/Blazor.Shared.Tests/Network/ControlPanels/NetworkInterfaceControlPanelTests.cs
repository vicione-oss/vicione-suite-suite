using Blazor.Shared.Network.ControlPanels.NetworkInterface.Components;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;
using Blazor.Shared.Network.Extensions;
using Blazor.Shared.Network.Services;
using Blazor.Shared.Settings.Extensions;
using Bunit;
using Core.Shared.HostManagement.Requests;
using Core.Shared.HostManagement.Services;
using HostManagement.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Xunit;

namespace Blazor.Shared.Tests.Network.ControlPanels;

public class NetworkInterfaceControlPanelTests
{
    [Fact]
    public void ComponentShouldRender()
    {
        // Arrange
        var timeProvider = Substitute.For<TimeProvider>();
        var systemConfigurationService = Substitute.For<ISystemConfigurationService>();
        timeProvider.LocalTimeZone.Returns(TimeZoneInfo.Utc);

        systemConfigurationService.SystemConfiguration.Returns(new SystemConfiguration());

        var controlPanelState = new NetworkInterfaceControlPanelState();
        var mediator = Substitute.For<IUiMediator>();

        mediator.Request<GetDHCPLeaseInformation, GetDHCPLeaseInformationResponse>(Arg.Any<GetDHCPLeaseInformation>(), Arg.Any<CancellationToken>())
            .Returns(new GetDHCPLeaseInformationResponse());

        using var ctx = new BunitContext();
        ctx.SetupBlazorSharedSettings(setup =>
        {
            setup.Services.AddSingleton(systemConfigurationService);
            setup.Services.AddKeyedScoped(Sdk.Constants.ClientTimeProviderServiceKey, (_, __) => timeProvider);

            setup.Services.AddControlPanelInfrastructure();
            setup.Services.AddNetwork();
            setup.Services.AddSingleton(mediator);
        });

        var registry = ctx.Services.GetRequiredService<IControlPanelRegistry<SharedClientModule>>();

        var registryItem = registry.Add<NetworkInterfaceControlPanel, NetworkInterfaceControlPanelState>(
            new NetworkInterfaceControlPanelDescriptor(controlPanelState, systemConfigurationService),
            controlPanelState,
            new ControlPanelNetworkCategoryDescriptor());

        // Act
        var component = ctx.Render<NetworkInterfaceControlPanel>(builder => builder
            .Add(p => p.State, controlPanelState)
            .AddCascadingValue(registryItem));

        // Assert
        Assert.NotNull(component);
    }
}
