using Core.OS.Instance.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Core.OS.Instance.HealthCheck;

public sealed class SyncRetryHealthCheck(SyncRetryState syncRetryState) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (syncRetryState.IsDegraded)
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Full-sync retry limit exhausted after {syncRetryState.AttemptCount} attempts. Manual intervention required."));

        if (syncRetryState.AttemptCount > 0)
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Full-sync failed {syncRetryState.AttemptCount} time(s), retrying..."));

        return Task.FromResult(HealthCheckResult.Healthy("No sync failures."));
    }
}
