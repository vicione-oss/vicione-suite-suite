using Core.OS.Instance.HealthCheck;
using Core.OS.Instance.Services;
using Core.OS.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sdk.Backend.Persistence;
using Sdk.Instance;

namespace Core.OS.Instance.Extensions;

public static class IHealthChecksBuilderExtensions
{
    extension(IHealthChecksBuilder hcBuilder)
    {
        public IHealthChecksBuilder AddInstanceHealthChecks(InstanceOptions instanceOptions)
        {
            //Health checks (maybe more in the future): 
            hcBuilder
                .AddSynchronizationHealthCheck()
                .AddCheck<MasterReachableHealthCheck>("MasterReachable");

            if (instanceOptions.Type == InstanceType.Master)
                hcBuilder.AddNpgSql(s => s.GetRequiredService<IMasterDbConnectionStringProvider>().ConnectionString);

            if (instanceOptions.Type == InstanceType.Slave)
            {
                hcBuilder.AddReplicationLagHealthCheck();
                hcBuilder.AddSyncRetryHealthCheck();
            }

            return hcBuilder;
        }

        private IHealthChecksBuilder AddSynchronizationHealthCheck()
            => hcBuilder.Add(new HealthCheckRegistration("Synchronization", s => new SuiteSynchronizationHealthCheck(s.GetRequiredService<SynchronizationState>()), null, null));

        private IHealthChecksBuilder AddReplicationLagHealthCheck()
            => hcBuilder.Add(new HealthCheckRegistration("ReplicationLag", s => new ReplicationLagHealthCheck(s.GetRequiredService<ReplicationLagTracker>()), null, null));

        private IHealthChecksBuilder AddSyncRetryHealthCheck()
            => hcBuilder.Add(new HealthCheckRegistration("SyncRetry", s => new SyncRetryHealthCheck(s.GetRequiredService<SyncRetryState>()), null, null));
    }

    //Needs to be added this way, because the creation of this HealthCheck in combination with SynchronizationState causes issues inside the .NET Code.
}
