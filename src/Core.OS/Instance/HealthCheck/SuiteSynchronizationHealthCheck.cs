using Core.OS.Instance.Contracts;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Core.OS.Instance.HealthCheck;

public sealed class SuiteSynchronizationHealthCheck(SynchronizationState synchronizationState) : IHealthCheck
{
    private readonly SynchronizationState _synchronizationState = synchronizationState;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(_synchronizationState.SynchronizationSucceeded
        ? HealthCheckResult.Healthy("Suite is synchronized")
        : HealthCheckResult.Unhealthy("Suite is not yet synchronized"));
}
