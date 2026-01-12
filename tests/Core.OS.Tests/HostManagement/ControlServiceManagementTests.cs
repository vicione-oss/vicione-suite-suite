using Core.OS.HostManagement;
using Core.OS.Tests.HostManagement.Extensions;
using Core.Shared.HostManagement.Services;
using AwesomeAssertions;
using HostManagement.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.SystemConfiguration;
using Sdk.SystemConfiguration.Contracts.Service;
using Xunit;

namespace Core.OS.Tests.HostManagement;

public sealed class ControlServiceManagementTests
{
    private const string ServiceName = "ControlServiceTest";

    private static ServiceProvider CreateServices()
    {
        var systemConfigurationService = Substitute.For<ISystemConfigurationService>();

        systemConfigurationService.SystemConfiguration.Returns(new SystemConfiguration());

        return new ServiceCollection()
            .AddSingleton<ControlServiceManagement>()
            .AddSingleton(Substitute.For<ILogger<ControlServiceManagement>>())
            .AddSingleton<SystemConfigurationCache>()
            .AddSingleton(Substitute.For<ILogger<SystemConfigurationCache>>())
            .AddSingleton(systemConfigurationService)
            .AddMockPipeClientSystemConfiguration()
            .BuildServiceProvider();
    }

    [Fact]
    public async Task Should_enable_unknown_service()
    {
        // Arrange
        await using var services = CreateServices();
        var service = services.GetRequiredService<ControlServiceManagement>();

        // Act
        var result = await service.ControlService(ServiceCommand.Enable, ServiceName);

        // Assert
        result.Success.Should().BeTrue();
        result.ServiceName.Should().Be(ServiceName);
        result.State.Should().Be(ServiceState.Enabled);
    }

    [Fact]
    public async Task Should_disable_unknown_service()
    {
        // Arrange
        await using var services = CreateServices();
        var service = services.GetRequiredService<ControlServiceManagement>();

        // Act
        var result = await service.ControlService(ServiceCommand.Disable, ServiceName);

        // Assert
        result.Success.Should().BeTrue();
        result.ServiceName.Should().Be(ServiceName);
        result.State.Should().Be(ServiceState.Disabled);
    }

    [Fact]
    public async Task Should_restart_unknown_service()
    {
        // Arrange
        await using var services = CreateServices();
        var service = services.GetRequiredService<ControlServiceManagement>();

        // Act
        var result = await service.ControlService(ServiceCommand.Restart, ServiceName);

        // Assert
        result.Success.Should().BeTrue();
        result.ServiceName.Should().Be(ServiceName);
        result.State.Should().Be(ServiceState.Enabled);
    }

    [Fact]
    public async Task Should_start_unknown_service()
    {
        // Arrange
        await using var services = CreateServices();
        var service = services.GetRequiredService<ControlServiceManagement>();

        // Act
        var result = await service.ControlService(ServiceCommand.Start, ServiceName);

        // Assert
        result.Success.Should().BeTrue();
        result.ServiceName.Should().Be(ServiceName);
        result.State.Should().Be(ServiceState.Enabled);
    }

    [Fact]
    public async Task Should_stop_unknown_service()
    {
        // Arrange
        await using var services = CreateServices();
        var service = services.GetRequiredService<ControlServiceManagement>();

        // Act
        var result = await service.ControlService(ServiceCommand.Stop, ServiceName);

        // Assert
        result.Success.Should().BeTrue();
        result.ServiceName.Should().Be(ServiceName);
        result.State.Should().Be(ServiceState.Disabled);
    }
}
