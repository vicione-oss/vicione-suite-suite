using Core.OS.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Core.OS.Instance.HealthCheck;

/// <summary>
///     Reports health based on replication lag between master and this slave instance.
///     Degraded when any context exceeds <see cref="DegradedThreshold" />,
///     Unhealthy when any context exceeds <see cref="UnhealthyThreshold" />.
///     NTP synchronization between master and slave is assumed.
/// </summary>
public sealed class ReplicationLagHealthCheck(ReplicationLagTracker lagTracker) : IHealthCheck
{
    private static readonly TimeSpan DegradedThreshold = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan UnhealthyThreshold = TimeSpan.FromMinutes(5);

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var lags = lagTracker.GetAllLags();

        if (lags.Count == 0)
            return Task.FromResult(HealthCheckResult.Healthy("No replication data received yet."));

        var maxLag = TimeSpan.Zero;
        string? maxLagContext = null;

        foreach (var (contextType, lag) in lags)
        {
            if (lag > maxLag)
            {
                maxLag = lag;
                maxLagContext = contextType;
            }
        }

        var data = new Dictionary<string, object>();
        foreach (var (contextType, lag) in lags)
            data[contextType] = lag.TotalSeconds;

        if (maxLag >= UnhealthyThreshold)
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Replication lag for '{maxLagContext}' is {maxLag.TotalSeconds:F1}s (threshold: {UnhealthyThreshold.TotalSeconds}s).",
                data: data));

        if (maxLag >= DegradedThreshold)
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Replication lag for '{maxLagContext}' is {maxLag.TotalSeconds:F1}s (threshold: {DegradedThreshold.TotalSeconds}s).",
                data: data));

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Replication lag is within acceptable bounds (max: {maxLag.TotalSeconds:F1}s).",
            data));
    }
}
