using Core.Shared.Instance.HealthCheck;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Core.OS.Instance.HealthCheck;

public class MasterReachableHealthCheck(IMasterHealthInfo masterHealthInfo) : IHealthCheck
{
    private readonly IMasterHealthInfo _masterHealthInfo = masterHealthInfo;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        => _masterHealthInfo.IsMasterReachable ? Task.FromResult(HealthCheckResult.Healthy()) : Task.FromResult(HealthCheckResult.Degraded());
}
