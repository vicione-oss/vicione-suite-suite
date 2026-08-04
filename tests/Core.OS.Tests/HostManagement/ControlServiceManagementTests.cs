using Core.OS.HostManagement;
using Core.OS.Tests.HostManagement.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.SystemConfiguration.Commands;
using Sdk.SystemConfiguration.Contracts;

namespace Core.OS.Tests.HostManagement;

public sealed class ControlServiceManagementTests
{
    private const string ServiceName = "ControlServiceTest";

    private static ServiceProvider CreateServices()
    {
        return new ServiceCollection()
            .AddSingleton<ControlServiceManagement>()
            .AddSingleton(Substitute.For<ILogger<ControlServiceManagement>>())
            .AddSingleton<SystemConfigurationCache>()
            .AddSingleton(Substitute.For<ILogger<SystemConfigurationCache>>())
            .AddMockPipeClientSystemConfiguration()
            .BuildServiceProvider();
    }

    [Fact]
    public async Task Should_be_available()
    {
        // Arrange + Act
        await using var services = CreateServices();
        var service = services.GetRequiredService<ControlServiceManagement>();

        // Assert
        service.IsAvailable.Should().BeTrue();
    }
    
    [Fact]
    public async Task Should_enable_unknown_service()
    {
        // Arrange
        await using var services = CreateServices();
        var service = services.GetRequiredService<ControlServiceManagement>();

        // Act
        var result = await service.TryControlService(ServiceCommand.Enable, ServiceName, TestContext.Current.CancellationToken);

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
        var result = await service.TryControlService(ServiceCommand.Disable, ServiceName, TestContext.Current.CancellationToken);

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
        var result = await service.TryControlService(ServiceCommand.Restart, ServiceName, TestContext.Current.CancellationToken);

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
        var result = await service.TryControlService(ServiceCommand.Start, ServiceName, TestContext.Current.CancellationToken);

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
        var result = await service.TryControlService(ServiceCommand.Stop, ServiceName, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.ServiceName.Should().Be(ServiceName);
        result.State.Should().Be(ServiceState.Disabled);
    }
}
