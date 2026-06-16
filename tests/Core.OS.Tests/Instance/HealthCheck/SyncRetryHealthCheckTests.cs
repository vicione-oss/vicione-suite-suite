using AwesomeAssertions;
using Core.OS.Instance.HealthCheck;
using Core.OS.Instance.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Core.OS.Tests.Instance.HealthCheck;

public class SyncRetryHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_should_return_healthy_when_no_failures()
    {
        // Arrange
        var state = new SyncRetryState();
        var healthCheck = new SyncRetryHealthCheck(state);

        // Act
        var result = await healthCheck.CheckHealthAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_should_return_degraded_when_failures_below_limit()
    {
        // Arrange
        var state = new SyncRetryState();
        state.RecordFailure();
        var healthCheck = new SyncRetryHealthCheck(state);

        // Act
        var result = await healthCheck.CheckHealthAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public async Task CheckHealthAsync_should_return_unhealthy_when_retries_exhausted()
    {
        // Arrange
        var state = new SyncRetryState();
        state.RecordFailure(); // 1
        state.RecordFailure(); // 2
        state.RecordFailure(); // 3 — exhausted
        var healthCheck = new SyncRetryHealthCheck(state);

        // Act
        var result = await healthCheck.CheckHealthAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_should_return_healthy_after_reset()
    {
        // Arrange
        var state = new SyncRetryState();
        state.RecordFailure();
        state.RecordFailure();
        state.RecordFailure();
        state.Reset();
        var healthCheck = new SyncRetryHealthCheck(state);

        // Act
        var result = await healthCheck.CheckHealthAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }
}
