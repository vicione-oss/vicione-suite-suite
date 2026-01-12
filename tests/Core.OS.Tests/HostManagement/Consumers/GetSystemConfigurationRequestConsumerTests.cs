using Core.OS.HostManagement;
using Core.OS.HostManagement.Consumers;
using Core.OS.Tests.HostManagement.Extensions;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.SystemConfiguration.Requests;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.HostManagement.Consumers;

public sealed class GetSystemConfigurationRequestConsumerTests
{
    [Fact]
    public async Task Should_return_system_configuration()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<GetSystemConfigurationRequestConsumer>();
            cfg.AddSingleton<SystemConfigurationCache>();
            cfg.AddSingleton(Substitute.For<ILogger<SystemConfigurationCache>>());
            cfg.AddMockPipeClientSystemConfiguration();
        });
        var request = new GetSystemConfiguration();

        // Act
        var response = await tester.TestRequest<GetSystemConfigurationResponse, GetSystemConfiguration>(request);

        // Assert
        response.Should().NotBeNull();
        response.Configuration.Should().NotBeNull();
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Should_return_response_with_error_info_on_exception()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<GetSystemConfigurationRequestConsumer>();
            cfg.AddSingleton<SystemConfigurationCache>();
            cfg.AddSingleton(Substitute.For<ILogger<SystemConfigurationCache>>());
            cfg.AddSingleton(Substitute.For<IPipeClient>());
        });
        var request = new GetSystemConfiguration();

        // Act
        var response = await tester.TestRequest<GetSystemConfigurationResponse, GetSystemConfiguration>(request);

        // Assert
        response.Should().NotBeNull();
        response.Configuration.Should().BeNull();
        response.RequestError.Should().NotBeNull();
    }
}
