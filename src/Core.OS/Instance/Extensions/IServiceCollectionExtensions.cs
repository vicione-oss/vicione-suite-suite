using Core.OS.Instance.HealthCheck;
using Core.OS.Instance.Services;
using Core.OS.Persistence;
using Core.OS.UserManagement.Services;
using Core.Shared.Instance.HealthCheck;
using Core.Shared.Instance.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sdk.Backend.Persistence;
using Sdk.Instance;

namespace Core.OS.Instance.Extensions;

internal static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddInstanceServices(InstanceOptions instanceOptions, bool useInMemoryBus)
        {
            if (instanceOptions.Type == InstanceType.Master)
            {
                // A master without a real transport cannot reach a slave at all, so replication always has a
                // Bus Outbox to stage into.
                services
                    .AddTransient<IMasterDbConnectionStringProvider, MasterDbConnectionStringProvider>()
                    .AddTransient<IInstanceConfigurationRepository, InstanceConfigurationRepository>()
                    .AddSingleton<ReplicationSequenceCounter>()
                    .AddScoped<ISaveChangesInterceptor, ChangeTrackingInterceptor>()
                    .AddScoped<IReplicationPublisher, BusOutboxReplicationPublisher>();
            }

            services.AddSingleton<MasterHealthInfo>();

            if (!useInMemoryBus && instanceOptions.Type is InstanceType.Master or InstanceType.Slave)
            {
                services
                    .AddSingleton<IHealthCheckPublisher, InstanceHealthCheckPublisher>()
                    .AddSingleton<IMasterHealthService, MasterHealthService>()
                    .AddSingleton<IMasterHealthInfo>(s => s.GetRequiredService<MasterHealthInfo>());
            }
            else if (instanceOptions.Type is InstanceType.Standalone)
            {
                services.AddSingleton<IMasterHealthService, MockMasterHealthService>()
                    .AddSingleton<IMasterHealthInfo>(s =>
                    {
                        var info = s.GetRequiredService<MasterHealthInfo>();
                        info.IsMasterReachable = true;

                        return info;
                    });
            }

            services.AddHostedService<InstanceRecoveryService>();

            // User ticket store
            services.AddHostedService<UserTicketCleanupService>();
            services.AddSingleton<ITicketStore, UserTicketStore>();

            // Backup & Restore
            services
                .AddSingleton<IBackupStore, BackupStore>()
                .AddTransient<IBackupFactory, BackupFactory>();

            services.AddArtifactRepositoryServices();

            // General instance services
            return services.AddSingleton<ILocalInstanceInformationProvider, LocalInstanceInformationProvider>()
                .AddScoped<IInstanceInformationProvider, InstanceInformationProvider>()
                .AddSingleton<InMemoryClusterInformationProvider>()
                .AddSingleton<IClusterInformationProvider, InMemoryClusterInformationProvider>(p => p.GetRequiredService<InMemoryClusterInformationProvider>())
                .AddSingleton<SynchronizationState>()
                .AddSingleton<SyncRetryState>()
                .AddSingleton<ReplicationSequenceTracker>()
                .AddSingleton<ReplicationLagTracker>()
                .AddScoped<ILoginDesignService, LoginDesignService>()
                .AddScoped<INonceStore, NonceStore>()
                .AddScoped<IOnboardingStateStore, OnboardingStateStore>();
        }

        private IServiceCollection AddArtifactRepositoryServices()
        {
            services.AddTransient<IArtifactRepositoryStore, ArtifactRepositoryStore>();
            services.AddScoped<IArtifactRepositoryTokenService, ArtifactRepositoryTokenService>();
            services.AddHostedService<ArtifactRepositoryTokenUpdateService>();

            return services;
        }
    }
}
