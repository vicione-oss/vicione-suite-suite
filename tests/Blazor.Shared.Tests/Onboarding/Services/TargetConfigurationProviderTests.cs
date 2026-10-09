using Blazor.Shared.Onboarding.Services;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using Core.Shared.HostManagement;
using HostManagement.Shared.Contracts;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Tests.Onboarding.Services;

public sealed class TargetConfigurationProviderTests
{
    private static readonly TimeZoneInfo s_localTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();

    public TargetConfigurationProviderTests()
    {
        SetupCurrentSystemConfiguration(SystemConfigurations.CreateDhcp("edge-s-01"));

        _timeProvider.LocalTimeZone.Returns(s_localTimeZone);
    }

    private void SetupCurrentSystemConfiguration(SystemConfiguration? systemConfiguration)
        => _mediator
            .Request<GetHostMgmtSystemConfiguration, GetHostMgmtSystemConfigurationResponse>(
                Arg.Any<GetHostMgmtSystemConfiguration>(), Arg.Any<CancellationToken>())
            .Returns(new GetHostMgmtSystemConfigurationResponse { Configuration = systemConfiguration });

    private TargetConfigurationProvider CreateProvider() =>
        new(_mediator, _timeProvider);

    [Fact]
    public async Task Should_prepopulate_from_current_system_configuration()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var targetConfiguration = await provider.GetTargetConfiguration(TestContext.Current.CancellationToken);

        // Assert
        targetConfiguration.Hostname.Should().Be("edge-s-01");
        targetConfiguration.TimeZone.Should().Be(s_localTimeZone);
        targetConfiguration.LocalNetwork.ConfigurationMode.Should().Be(IpConfigurationMode.AutomaticDhcp);
        targetConfiguration.InternetConnection.ConfigurationMode.Should().Be(IpConfigurationMode.AutomaticDhcp);
        targetConfiguration.UserCredentials.Should().BeNull();
    }

    [Fact]
    public async Task Should_build_target_configuration_only_once()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var first = await provider.GetTargetConfiguration(TestContext.Current.CancellationToken);
        var second = await provider.GetTargetConfiguration(TestContext.Current.CancellationToken);

        // Assert
        second.Should().BeSameAs(first);
        await _mediator.Received(1).Request<GetHostMgmtSystemConfiguration, GetHostMgmtSystemConfigurationResponse>(
            Arg.Any<GetHostMgmtSystemConfiguration>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_set_local_time_zone_without_system_configuration()
    {
        // Arrange
        SetupCurrentSystemConfiguration(null);
        var provider = CreateProvider();

        // Act
        var targetConfiguration = await provider.GetTargetConfiguration(TestContext.Current.CancellationToken);

        // Assert
        targetConfiguration.TimeZone.Should().Be(s_localTimeZone);
        targetConfiguration.Hostname.Should().BeEmpty();
    }
}
