using Core.OS.Instance.HealthCheck;
using Core.OS.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Core.OS.Tests.Persistence;

public sealed class ReplicationLagHealthCheckTests
{
    [Fact]
    public async Task Should_report_healthy_when_no_data_received()
    {
        // Arrange
        var lagTracker = new ReplicationLagTracker();
        var healthCheck = new ReplicationLagHealthCheck(lagTracker);

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Should_report_healthy_when_lag_below_degraded_threshold()
    {
        // Arrange
        var lagTracker = new ReplicationLagTracker();
        lagTracker.Record("TestContext", DateTimeOffset.UtcNow - TimeSpan.FromSeconds(5));
        var healthCheck = new ReplicationLagHealthCheck(lagTracker);

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Should_report_degraded_when_lag_exceeds_30_seconds()
    {
        // Arrange
        var lagTracker = new ReplicationLagTracker();
        lagTracker.Record("TestContext", DateTimeOffset.UtcNow - TimeSpan.FromSeconds(45));
        var healthCheck = new ReplicationLagHealthCheck(lagTracker);

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public async Task Should_report_unhealthy_when_lag_exceeds_5_minutes()
    {
        // Arrange
        var lagTracker = new ReplicationLagTracker();
        lagTracker.Record("TestContext", DateTimeOffset.UtcNow - TimeSpan.FromMinutes(6));
        var healthCheck = new ReplicationLagHealthCheck(lagTracker);

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task Should_report_based_on_worst_context_lag()
    {
        // Arrange
        var lagTracker = new ReplicationLagTracker();
        lagTracker.Record("ContextA", DateTimeOffset.UtcNow - TimeSpan.FromSeconds(5)); // Healthy
        lagTracker.Record("ContextB", DateTimeOffset.UtcNow - TimeSpan.FromSeconds(45)); // Degraded
        var healthCheck = new ReplicationLagHealthCheck(lagTracker);

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public async Task Should_include_lag_data_per_context()
    {
        // Arrange
        var lagTracker = new ReplicationLagTracker();
        lagTracker.Record("ContextA", DateTimeOffset.UtcNow - TimeSpan.FromSeconds(10));
        lagTracker.Record("ContextB", DateTimeOffset.UtcNow - TimeSpan.FromSeconds(20));
        var healthCheck = new ReplicationLagHealthCheck(lagTracker);

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        result.Data.Should().ContainKey("ContextA");
        result.Data.Should().ContainKey("ContextB");
    }

    private static HealthCheckContext CreateContext() => new()
    {
        Registration = new HealthCheckRegistration("ReplicationLag", _ => new ReplicationLagHealthCheck(new ReplicationLagTracker()), null, null)
    };
}
