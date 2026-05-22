using AwesomeAssertions;
using Blazor.Shared.Network.ControlPanels.Ntp.Services;
using Blazor.Shared.Services;
using Core.Shared.HostManagement.Requests;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Xunit;

namespace Blazor.Shared.Tests.Network.ControlPanels.Ntp.Services;

public sealed class NtpControlPanelResetHandlerTests
{
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly ISystemConfigurationService _systemConfigurationService = Substitute.For<ISystemConfigurationService>();

    public NtpControlPanelResetHandlerTests()
    {
        _mediator.Request<GetNTPFallbackInformation, GetNTPFallbackInformationResponse>(
                Arg.Any<GetNTPFallbackInformation>(), Arg.Any<CancellationToken>())
            .Returns(new GetNTPFallbackInformationResponse());
    }

    private ServiceProvider SetupServiceProvider()
    {
        var services = new ServiceCollection()
            .AddScoped(_ => _mediator)
            .AddScoped(_ => _systemConfigurationService)
            .AddScoped<IControlPanelResetHandler<NtpControlPanelState>, NtpControlPanelResetHandler>();

        return services.BuildServiceProvider();
    }

    private void SetupSystemConfiguration(NetworkNTPSettings ntpSettings)
    {
        _systemConfigurationService.SystemConfiguration.Returns(new SystemConfiguration
        {
            NetworkNTPSettings = ntpSettings
        });
    }

    [Fact]
    public async Task Should_end_loading_after_reset()
    {
        // Arrange
        SetupSystemConfiguration(new NetworkNTPSettings());
        await using var serviceProvider = SetupServiceProvider();

        var state = new NtpControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<NtpControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task Should_set_ntp_servers_enabled_from_system_configuration()
    {
        // Arrange
        SetupSystemConfiguration(new NetworkNTPSettings { NTPServersEnabled = true });
        await using var serviceProvider = SetupServiceProvider();

        var state = new NtpControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<NtpControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.NtpServersEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Should_set_ntp_server_details_from_system_configuration()
    {
        // Arrange
        SetupSystemConfiguration(new NetworkNTPSettings(ntpServers: ["pool.ntp.org", "time.windows.com"]));
        await using var serviceProvider = SetupServiceProvider();

        var state = new NtpControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<NtpControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.NtpServerDetails.Should().HaveCount(2);
        state.NtpServerDetails[0].IpAddressOrHostname.Should().Be("pool.ntp.org");
        state.NtpServerDetails[1].IpAddressOrHostname.Should().Be("time.windows.com");
    }

    [Fact]
    public async Task Should_have_one_empty_ntp_server_detail_when_no_servers_configured()
    {
        // Arrange
        SetupSystemConfiguration(new NetworkNTPSettings());
        await using var serviceProvider = SetupServiceProvider();

        var state = new NtpControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<NtpControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.NtpServerDetails.Should().HaveCount(1);
        state.NtpServerDetails[0].IpAddressOrHostname.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_set_fallback_ntp_server_details_from_mediator_response()
    {
        // Arrange
        SetupSystemConfiguration(new NetworkNTPSettings());
        _mediator.Request<GetNTPFallbackInformation, GetNTPFallbackInformationResponse>(
                Arg.Any<GetNTPFallbackInformation>(), Arg.Any<CancellationToken>())
            .Returns(new GetNTPFallbackInformationResponse { FallbackNTPServers = ["fallback1.ntp.org", "fallback2.ntp.org"] });

        await using var serviceProvider = SetupServiceProvider();

        var state = new NtpControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<NtpControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.FallbackNtpServerDetails.Should().HaveCount(2);
        state.FallbackNtpServerDetails[0].IpAddressOrHostname.Should().Be("fallback1.ntp.org");
        state.FallbackNtpServerDetails[1].IpAddressOrHostname.Should().Be("fallback2.ntp.org");
    }

    [Fact]
    public async Task Should_have_one_empty_fallback_ntp_server_detail_when_mediator_returns_null()
    {
        // Arrange
        SetupSystemConfiguration(new NetworkNTPSettings());
        _mediator.Request<GetNTPFallbackInformation, GetNTPFallbackInformationResponse>(
                Arg.Any<GetNTPFallbackInformation>(), Arg.Any<CancellationToken>())
            .Returns(new GetNTPFallbackInformationResponse { FallbackNTPServers = null });

        await using var serviceProvider = SetupServiceProvider();

        var state = new NtpControlPanelState();
        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<NtpControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.FallbackNtpServerDetails.Should().HaveCount(1);
        state.FallbackNtpServerDetails[0].IpAddressOrHostname.Should().BeEmpty();
    }
}
