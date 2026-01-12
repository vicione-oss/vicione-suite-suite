using AwesomeAssertions;
using Core.OS.HostManagement;
using Core.OS.HostManagement.Consumers;
using Core.OS.Tests.HostManagement.Extensions;
using Core.Shared.HostManagement.Requests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.HostManagement.Consumers;

public class GetNTPFallbackInformationConsumerTests
{
    [Fact]
    public async Task Should_return_ntp_fallback_information()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<GetNTPFallbackInformationConsumer>();
            cfg.AddSingleton<SystemConfigurationCache>();
            cfg.AddSingleton(Substitute.For<ILogger<SystemConfigurationCache>>());
            cfg.AddMockPipeClientSystemConfiguration();
        });
        var request = new GetNTPFallbackInformation();

        // Act
        var response = await tester.TestRequest<GetNTPFallbackInformationResponse, GetNTPFallbackInformation>(request);

        // Assert
        response.Should().NotBeNull();
        response.FallbackNTPServers.Should().NotBeNull();
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Should_return_response_with_error_info_on_exception()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<GetNTPFallbackInformationConsumer>();
            cfg.AddSingleton<SystemConfigurationCache>();
            cfg.AddSingleton(Substitute.For<ILogger<SystemConfigurationCache>>());
            cfg.AddSingleton(Substitute.For<IPipeClient>());
        });
        var request = new GetNTPFallbackInformation();

        // Act
        var response = await tester.TestRequest<GetNTPFallbackInformationResponse, GetNTPFallbackInformation>(request);

        // Assert
        response.Should().NotBeNull();
        response.FallbackNTPServers.Should().BeNull();
        response.RequestError.Should().NotBeNull();
    }
}

