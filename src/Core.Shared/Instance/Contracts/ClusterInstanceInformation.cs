using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sdk.Instance;

namespace Core.Shared.Instance.Contracts;

public sealed class ClusterInstanceInformation(IInstanceInformation instanceInformation)
{
    public IInstanceInformation InstanceInformation { get; } = instanceInformation;
    public HealthStatus? HealthStatus { get; set; }
}
