using Blazor.Shared.Network.Services.Validators;
using Blazor.Shared.Onboarding.Models;
using Blazor.Shared.Onboarding.Services;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using Blazor.Shared.Validation.Services.Validators;
using Core.Shared.HostManagement;
using HostManagement.Shared.Contracts;
using Sdk.Client.Infrastructure;
using Sdk.Client.Wizards.Models;

namespace Blazor.Shared.Tests.Onboarding.Services;

public sealed class NetworkWizardPageSaveHandlerTests
{
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly ITargetConfigurationProvider _targetConfigurationProvider = Substitute.For<ITargetConfigurationProvider>();
    private readonly TargetConfiguration _targetConfiguration = new();

    public NetworkWizardPageSaveHandlerTests()
    {
        SetupCurrentSystemConfiguration(SystemConfigurations.CreateDhcp());

        _targetConfigurationProvider
            .GetTargetConfiguration(Arg.Any<CancellationToken>())
            .Returns(_targetConfiguration);
    }

    private void SetupCurrentSystemConfiguration(SystemConfiguration? systemConfiguration)
        => _mediator
            .Request<GetHostMgmtSystemConfiguration, GetHostMgmtSystemConfigurationResponse>(
                Arg.Any<GetHostMgmtSystemConfiguration>(), Arg.Any<CancellationToken>())
            .Returns(new GetHostMgmtSystemConfigurationResponse { Configuration = systemConfiguration });

    private NetworkWizardPageSaveHandler CreateHandler() =>
        new(_mediator, new RequiredValidator(), new IpAddressValidator(), _targetConfigurationProvider);

    private static NetworkWizardPageState CreateManualLocalNetworkState(string ipAddress = "192.168.1.10",
        string subnetMask = "255.255.255.0", string? defaultGateway = null, string? dnsServer = null)
    {
        var state = new NetworkWizardPageState();

        state.InternetConnection.ConfigurationMode = IpConfigurationMode.AutomaticDhcp;

        state.LocalNetwork.ConfigurationMode = IpConfigurationMode.Manual;
        state.LocalNetwork.IpAddress = ipAddress;
        state.LocalNetwork.SubnetMask = subnetMask;
        state.LocalNetwork.DefaultGateway = defaultGateway;
        state.LocalNetwork.DnsServer = dnsServer;

        return state;
    }

    [Fact]
    public async Task Should_store_manual_configuration_in_target_configuration()
    {
        // Arrange
        var handler = CreateHandler();
        var state = CreateManualLocalNetworkState(defaultGateway: "192.168.1.1", dnsServer: "192.168.1.2");

        // Act
        var result = await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        _targetConfiguration.LocalNetwork.Should().BeEquivalentTo(state.LocalNetwork);
        _targetConfiguration.InternetConnection.Should().BeEquivalentTo(state.InternetConnection);
    }

    [Fact]
    public async Task Should_accept_manual_configuration_without_gateway_and_dns_server()
    {
        // Arrange
        var handler = CreateHandler();
        var state = CreateManualLocalNetworkState();

        // Act
        var result = await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
    }

    [Fact]
    public async Task Should_skip_field_validation_in_dhcp_mode()
    {
        // Arrange
        var handler = CreateHandler();
        var state = new NetworkWizardPageState();
        state.LocalNetwork.ConfigurationMode = IpConfigurationMode.AutomaticDhcp;
        state.LocalNetwork.IpAddress = "not an ip address";

        // Act
        var result = await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
    }

    [Theory]
    [InlineData("", "255.255.255.0", null, null)]
    [InlineData("192.168.1.300", "255.255.255.0", null, null)]
    [InlineData("192.168.1.10", "", null, null)]
    [InlineData("192.168.1.10", "mask", null, null)]
    [InlineData("192.168.1.10", "255.255.255.0", "gateway", null)]
    [InlineData("192.168.1.10", "255.255.255.0", null, "dns")]
    public async Task Should_reject_manual_configuration_with_missing_or_malformed_field(string ipAddress, string subnetMask,
        string? defaultGateway, string? dnsServer)
    {
        // Arrange
        var handler = CreateHandler();
        var state = CreateManualLocalNetworkState(ipAddress, subnetMask, defaultGateway, dnsServer);

        // Act
        var result = await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
        await _mediator.DidNotReceive().Request<GetHostMgmtSystemConfiguration, GetHostMgmtSystemConfigurationResponse>(
            Arg.Any<GetHostMgmtSystemConfiguration>(), Arg.Any<CancellationToken>());
        _targetConfiguration.LocalNetwork.ConfigurationMode.Should().Be(IpConfigurationMode.AutomaticDhcp);
    }

    [Fact]
    public async Task Should_reject_when_current_system_configuration_is_unavailable()
    {
        // Arrange
        SetupCurrentSystemConfiguration(null);

        var handler = CreateHandler();
        var state = CreateManualLocalNetworkState();

        // Act
        var result = await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
        _targetConfiguration.LocalNetwork.ConfigurationMode.Should().Be(IpConfigurationMode.AutomaticDhcp);
        state.CurrentOperation.Should().BeNull();
    }

    [Fact]
    public async Task Should_reject_configuration_that_fails_system_configuration_validation()
    {
        // Arrange
        var handler = CreateHandler();
        var state = CreateManualLocalNetworkState(defaultGateway: "10.0.0.1");

        // Act
        var result = await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>()
            .Which.Message.Should().NotBeNullOrWhiteSpace();
        _targetConfiguration.LocalNetwork.ConfigurationMode.Should().Be(IpConfigurationMode.AutomaticDhcp);
        state.CurrentOperation.Should().BeNull();
    }
}
