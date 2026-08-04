using Core.OS.HostManagement;
using Core.OS.HostManagement.Consumers;
using Core.OS.Tests.HostManagement.Extensions;
using Core.Shared.HostManagement.Requests;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.HostManagement.Consumers;

public class GetDHCPLeaseInformationConsumerTests
{
    private static void ConfigureServices(IBusRegistrationConfigurator cfg, IPipeClient? pipeClient = null)
    {
        cfg.AddConsumer<GetDHCPLeaseInformationConsumer>();
        cfg.AddSingleton<SystemConfigurationCache>();
        if (pipeClient is not null)
        {
            cfg.AddSingleton(pipeClient);
            return;
        }
        cfg.AddMockPipeClientSystemConfiguration();
    }

    [Fact]
    public async Task Should_return_dhcp_lease_information()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg => ConfigureServices(cfg));
        var request = new GetDHCPLeaseInformation("eth0");

        // Act
        var response = await tester.TestRequest<GetDHCPLeaseInformationResponse, GetDHCPLeaseInformation>(request);

        // Assert
        response.Should().NotBeNull();
        response.DHCPLease.Should().NotBeNull();
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Should_return_response_with_error_info_on_exception()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg => ConfigureServices(cfg, Substitute.For<IPipeClient>()));
        var request = new GetDHCPLeaseInformation("eth0");

        // Act
        var response = await tester.TestRequest<GetDHCPLeaseInformationResponse, GetDHCPLeaseInformation>(request);

        // Assert
        response.Should().NotBeNull();
        response.DHCPLease.Should().BeNull();
        response.RequestError.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_use_fallback_response_on_deserialization_errors()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg => ConfigureServices(cfg));
        var request = new GetDHCPLeaseInformation("");

        // Act
        var response = await tester.TestRequest<GetDHCPLeaseInformationResponse, GetDHCPLeaseInformation>(request);

        // Assert
        response.Should().NotBeNull();
        response.DHCPLease.Should().BeNull();
        response.RequestError.Should().NotBeNull();
    }
}
