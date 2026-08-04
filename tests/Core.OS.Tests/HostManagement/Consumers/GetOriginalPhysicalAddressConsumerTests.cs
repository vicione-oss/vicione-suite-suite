using Core.OS.HostManagement;
using Core.OS.HostManagement.Consumers;
using Core.OS.Tests.HostManagement.Extensions;
using Core.Shared.HostManagement.Requests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.HostManagement.Consumers;

public sealed class GetOriginalPhysicalAddressConsumerTests
{
    [Fact]
    public async Task Should_return_original_physical_address()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<GetOriginalPhysicalAddressConsumer>();
            cfg.AddSingleton<SystemConfigurationCache>();
            cfg.AddSingleton(Substitute.For<ILogger<SystemConfigurationCache>>());
            cfg.AddMockPipeClientSystemConfiguration();
        });
        var request = new GetOriginalPhysicalAddress("eth0");

        // Act
        var response = await tester.TestRequest<GetOriginalPhysicalAddressResponse, GetOriginalPhysicalAddress>(request);

        // Assert
        response.Should().NotBeNull();
        response.OriginalPhysicalAddress.Should().NotBeNull();
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Should_return_response_with_error_info_on_exception()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<GetOriginalPhysicalAddressConsumer>();
            cfg.AddSingleton<SystemConfigurationCache>();
            cfg.AddSingleton(Substitute.For<ILogger<SystemConfigurationCache>>());
            cfg.AddSingleton(Substitute.For<IPipeClient>());
        });
        var request = new GetOriginalPhysicalAddress("eth0");

        // Act
        var response = await tester.TestRequest<GetOriginalPhysicalAddressResponse, GetOriginalPhysicalAddress>(request);

        // Assert
        response.Should().NotBeNull();
        response.OriginalPhysicalAddress.Should().BeNull();
        response.RequestError.Should().NotBeNull();
    }
}
