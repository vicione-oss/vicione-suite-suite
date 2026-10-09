using Blazor.Shared.Onboarding.Components.WizardPages;
using Blazor.Shared.Onboarding.Services;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using Blazor.Shared.Settings.Extensions;
using Bunit;
using SummaryWizardPageTexts = Blazor.Shared.Onboarding.Components.WizardPages.Localization.SummaryWizardPage;

namespace Blazor.Shared.Tests.Onboarding.Components.WizardPages;

public sealed class SummaryWizardPageTests : IAsyncDisposable
{
    private readonly BunitContext _ctx = new();

    public SummaryWizardPageTests()
        => _ctx.SetupBlazorSharedSettings(setup => setup.Services.AddControlPanelInfrastructure()).SetLocalServices();

    public ValueTask DisposeAsync()
        => _ctx.DisposeAsync();

    private IRenderedComponent<SummaryWizardPage> Render(SummaryWizardPageState state)
        => _ctx.Render<SummaryWizardPage>(builder => builder.Add(c => c.State, state));

    [Fact]
    public void Should_show_device_and_time_zone_values()
    {
        // Arrange
        var state = new SummaryWizardPageState
        {
            Hostname = "edge-s-01",
            SerialNumber = "SN-4711",
            TimeZone = "(UTC+01:00) Berlin",
            Date = "08.10.2026",
            Time = "14:30"
        };

        // Act
        var component = Render(state);

        // Assert
        component.Markup.Should().Contain("edge-s-01")
            .And.Contain("SN-4711")
            .And.Contain("(UTC+01:00) Berlin")
            .And.Contain("08.10.2026")
            .And.Contain("14:30");
    }

    [Fact]
    public void Should_show_details_of_both_network_interfaces()
    {
        // Arrange
        var state = new SummaryWizardPageState();

        state.InternetConnection.ConfigurationMode = IpConfigurationMode.Manual;
        state.InternetConnection.IpAddress = "10.0.0.5";

        state.LocalNetwork.ConfigurationMode = IpConfigurationMode.Manual;
        state.LocalNetwork.IpAddress = "192.168.1.10";

        // Act
        var component = Render(state);

        // Assert
        component.FindAll(".settings-network-interface-details").Should().HaveCount(2);
        component.Markup.Should().Contain("10.0.0.5").And.Contain("192.168.1.10");
    }

    [Fact]
    public void Should_not_announce_automatic_restart()
    {
        // Arrange
        var state = new SummaryWizardPageState();

        // Act
        var component = Render(state);

        // Assert
        component.Markup.Should().NotContain(SummaryWizardPageTexts.AutomaticRestartTitle);
    }
}
