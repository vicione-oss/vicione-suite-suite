using Core.OS.HostManagement;
using Core.OS.HostManagement.Consumers;
using Core.OS.Tests.HostManagement.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.NetworkStatus.Requests;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.HostManagement.Consumers;

public sealed class GetNetworkStatusInformationRequestConsumerTests
{
    [Fact]
    public async Task Should_return_network_status_information()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<GetNetworkStatusInformationRequestConsumer>();
            cfg.AddSingleton<SystemConfigurationCache>();
            cfg.AddSingleton(Substitute.For<ILogger<SystemConfigurationCache>>());
            cfg.AddMockPipeClientSystemConfiguration();
        });
        var request = new GetNetworkStatusInformation("eth0");

        // Act
        var response = await tester.TestRequest<GetNetworkStatusInformationResponse, GetNetworkStatusInformation>(request);

        // Assert
        response.Should().NotBeNull();
        response.NetworkStatusInformation.Should().NotBeNull();
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Should_return_response_with_error_info_on_exception()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<GetNetworkStatusInformationRequestConsumer>();
            cfg.AddSingleton<SystemConfigurationCache>();
            cfg.AddSingleton(Substitute.For<ILogger<SystemConfigurationCache>>());
            cfg.AddSingleton(Substitute.For<IPipeClient>());
        });
        var request = new GetNetworkStatusInformation("eth0");

        // Act
        var response = await tester.TestRequest<GetNetworkStatusInformationResponse, GetNetworkStatusInformation>(request);

        // Assert
        response.Should().NotBeNull();
        response.NetworkStatusInformation.Should().BeNull();
        response.RequestError.Should().NotBeNull();
    }
}
