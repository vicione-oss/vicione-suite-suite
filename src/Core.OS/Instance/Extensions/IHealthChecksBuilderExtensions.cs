using Core.OS.Instance.Contracts;
using Core.OS.Instance.HealthCheck;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sdk.Backend.Persistence;
using Sdk.Instance;

namespace Core.OS.Instance.Extensions;

public static class IHealthChecksBuilderExtensions
{
    public static IHealthChecksBuilder AddInstanceHealthChecks(this IHealthChecksBuilder hcBuilder, InstanceOptions instanceOptions)
    {
        //Health checks (maybe more in the future): 
        hcBuilder
            .AddSynchronizationHealthCheck()
            .AddCheck<MasterReachableHealthCheck>("MasterReachable");

        if (instanceOptions.Type == InstanceType.Master)
            hcBuilder.AddNpgSql(s => s.GetRequiredService<IMasterDbConnectionStringProvider>().ConnectionString);

        return hcBuilder;
    }

    //Needs to be added this way, because the createn of this HealthCheck in combination with SynchronizationState causes issues inside the .NET Code.
    private static IHealthChecksBuilder AddSynchronizationHealthCheck(this IHealthChecksBuilder builder)
        => builder.Add(new HealthCheckRegistration("Synchronization", s => new SuiteSynchronizationHealthCheck(s.GetRequiredService<SynchronizationState>()), null, null));
}
