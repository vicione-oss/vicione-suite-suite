using Blazor.Shared.Onboarding.Components.WizardPages;
using Blazor.Shared.Onboarding.Models;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using Bunit;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Tests.Onboarding.Components.WizardPages;

public sealed class SummaryNetworkInterfaceDetailsTests : IAsyncDisposable
{
    private readonly BunitContext _ctx = new();

    public ValueTask DisposeAsync()
        => _ctx.DisposeAsync();

    private IRenderedComponent<SummaryNetworkInterfaceDetails> Render(INetworkInterfaceConfiguration configuration)
        => _ctx.Render<SummaryNetworkInterfaceDetails>(builder => builder.Add(c => c.NetworkInterfaceConfiguration, configuration));

    [Fact]
    public void Should_show_address_details_in_manual_mode()
    {
        // Arrange
        var configuration = new NetworkInterfaceConfiguration(2)
        {
            ConfigurationMode = IpConfigurationMode.Manual,
            IpAddress = "192.168.1.10",
            SubnetMask = "255.255.255.0",
            DefaultGateway = "192.168.1.1",
            DnsServer = "192.168.1.2"
        };

        // Act
        var component = Render(configuration);

        // Assert
        component.Markup.Should().Contain(TechnicalTerms.IpAddress)
            .And.Contain("192.168.1.10")
            .And.Contain("255.255.255.0")
            .And.Contain("192.168.1.1")
            .And.Contain("192.168.1.2");
    }

    [Fact]
    public void Should_hide_address_details_in_dhcp_mode()
    {
        // Arrange
        var configuration = new NetworkInterfaceConfiguration(2)
        {
            ConfigurationMode = IpConfigurationMode.AutomaticDhcp,
            IpAddress = "192.168.1.10"
        };

        // Act
        var component = Render(configuration);

        // Assert
        component.Markup.Should().NotContain(TechnicalTerms.IpAddress)
            .And.NotContain("192.168.1.10");
    }
}
