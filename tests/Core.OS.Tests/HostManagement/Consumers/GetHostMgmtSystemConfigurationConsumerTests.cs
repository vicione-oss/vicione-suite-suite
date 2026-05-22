using Core.OS.HostManagement;
using Core.OS.HostManagement.Consumers;
using Core.OS.Tests.HostManagement.Extensions;
using Core.Shared.HostManagement;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.HostManagement.Consumers;

public class GetHostMgmtSystemConfigurationConsumerTests
{
    [Fact]
    public async Task ShouldReturnSystemConfiguration()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<GetHostMgmtSystemConfigurationConsumer>();
            cfg.AddSingleton<SystemConfigurationCache>();
            cfg.AddSingleton(Substitute.For<ILogger<SystemConfigurationCache>>());
            cfg.AddMockPipeClientSystemConfiguration();
        });

        var request = new GetHostMgmtSystemConfiguration();

        // Act
        var response = await tester.TestRequest<GetHostMgmtSystemConfigurationResponse, GetHostMgmtSystemConfiguration>(request);

        // Assert
        response.Should().NotBeNull();
        response.Configuration.Should().NotBeNull();
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task ShouldReturnResponseWithErrorInfoOnException()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<GetHostMgmtSystemConfigurationConsumer>();
            cfg.AddSingleton<SystemConfigurationCache>();
            cfg.AddSingleton(Substitute.For<ILogger<SystemConfigurationCache>>());
            cfg.AddSingleton(Substitute.For<IPipeClient>());
        });

        var request = new GetHostMgmtSystemConfiguration();

        // Act
        var response = await tester.TestRequest<GetHostMgmtSystemConfigurationResponse, GetHostMgmtSystemConfiguration>(request);

        // Assert
        response.Should().NotBeNull();
        response.Configuration.Should().BeNull();
        response.RequestError.Should().NotBeNull();
    }
}
