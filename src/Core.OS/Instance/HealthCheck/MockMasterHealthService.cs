using Core.Shared.Instance.HealthCheck;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Core.OS.Instance.HealthCheck;

public class MockMasterHealthService : IMasterHealthService
{
    public Task CheckHealthStatus(Guid id, HealthStatus healthStatus, CancellationToken token = default) => Task.CompletedTask;
}
