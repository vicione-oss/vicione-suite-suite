using Core.OS.HostManagement;
using Core.OS.HostManagement.Consumers;
using Core.OS.Tests.HostManagement.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.SystemConfiguration.Requests;
using Sdk.Testing.Backend;

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
    public async Task Should_return_original_physical_address_when_user_defined_mac_address_is_disabled()
    {
        // Arrange
        await using var tester = CreateTesterWithMockPipeClient();
        var request = new GetSystemConfiguration();

        // Act
        var response = await tester.TestRequest<GetSystemConfigurationResponse, GetSystemConfiguration>(request);

        // Assert
        response.Configuration!.NetworkInterfaces.Single(i => i.Name == "lan2").PhysicalAddress.Should().Be("00:02:01:10:53:25");
    }

    [Fact]
    public async Task Should_return_user_defined_mac_address_as_physical_address_when_enabled()
    {
        // Arrange
        await using var tester = CreateTesterWithMockPipeClient();
        var request = new GetSystemConfiguration();

        // Act
        var response = await tester.TestRequest<GetSystemConfigurationResponse, GetSystemConfiguration>(request);

        // Assert
        response.Configuration!.NetworkInterfaces.Single(i => i.Name == "lan1").PhysicalAddress.Should().Be("00:02:01:10:53:26");
    }

    [Fact]
    public async Task Should_not_expose_host_management_schema_version()
    {
        // Arrange
        await using var tester = CreateTesterWithMockPipeClient();
        var request = new GetSystemConfiguration();

        // Act
        var response = await tester.TestRequest<GetSystemConfigurationResponse, GetSystemConfiguration>(request);

        // Assert
        response.Configuration!.Version.Should().Be(new Sdk.SystemConfiguration.Contracts.SystemConfiguration().Version);
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

    private static MassTransitTester CreateTesterWithMockPipeClient()
        => new(cfg =>
        {
            cfg.AddConsumer<GetSystemConfigurationRequestConsumer>();
            cfg.AddSingleton<SystemConfigurationCache>();
            cfg.AddSingleton(Substitute.For<ILogger<SystemConfigurationCache>>());
            cfg.AddMockPipeClientSystemConfiguration();
        });
}
