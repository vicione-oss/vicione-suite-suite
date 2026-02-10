using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Core.Shared.Instance.HealthCheck;

public interface IMasterHealthService
{
    Task CheckHealthStatus(Guid id, HealthStatus healthStatus, CancellationToken token = default);
}
