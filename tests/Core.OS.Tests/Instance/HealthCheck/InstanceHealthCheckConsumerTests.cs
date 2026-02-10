using Core.OS.Instance.HealthCheck;
using Core.Shared.Instance.HealthCheck;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Instance.HealthCheck;

public class InstanceHealthCheckConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public InstanceHealthCheckConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<InstanceHealthCheckConsumer>();
            cfg.AddSingleton(Substitute.For<IMasterHealthService>());
        };

    [Fact]
    public async Task Calls_check_health_status()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var service = tester.Services.GetRequiredService<IMasterHealthService>();
        var command = new InstanceHealthInfo(Guid.Empty, DateTimeOffset.Now, HealthStatus.Healthy);

        // Act
        await tester.TestEvent<InstanceHealthInfo, InstanceHealthCheckConsumer>(command);

        // Assert
        await service.Received(1).CheckHealthStatus(Arg.Any<Guid>(), Arg.Any<HealthStatus>(), Arg.Any<CancellationToken>());
    }
}
