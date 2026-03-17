using Core.OS.Instance.Contracts;
using Core.OS.Instance.HealthCheck;
using Core.OS.Instance.Services;
using Core.OS.Modules;
using Core.OS.Persistence;
using Core.OS.UserManagement.Services;
using Core.Shared.Instance.HealthCheck;
using Core.Shared.Instance.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sdk.Backend.Modules;
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
                services
                    .AddTransient<IMasterDbConnectionStringProvider, MasterDbConnectionStringProvider>()
                    .AddTransient<IInstanceConfigurationRepository, InstanceConfigurationRepository>()
                    .AddScoped<ISaveChangesInterceptor, ChangeTrackingInterceptor>();
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

            // Artifact repository
            services.AddTransient<IArtifactRepositoryStore, ArtifactRepositoryStore>();
            services.AddScoped<IArtifactRepositoryTokenService, ArtifactRepositoryTokenService>();
            services.AddHostedService<ArtifactRepositoryTokenUpdateService>();

            // General instance services
            return services.AddSingleton<ILocalInstanceInformationProvider, LocalInstanceInformationProvider>()
                .AddScoped<IInstanceInformationProvider, InstanceInformationProvider>()
                .AddSingleton<InMemoryClusterInformationProvider>()
                .AddSingleton<IClusterInformationProvider, InMemoryClusterInformationProvider>(p => p.GetRequiredService<InMemoryClusterInformationProvider>())
                .AddSingleton<SynchronizationState>()
                .AddScoped<INonceStore, NonceStore>()
                .AddScoped<IOnboardingStateStore, OnboardingStateStore>()
                .AddStreamUploadHandler<SystemBackendModule, DeviceImageContext>(options => options.FilenameTransform = filename => Shared.Constants.DeviceImageFileName);
        }

        private IServiceCollection AddStreamUploadHandler<TModule, TContext>(Action<StreamUploadHandlerOptions<TContext>>? configureOptions = null)
            where TModule : BackendModule
        {
            if (configureOptions is not null)
                services.Configure(configureOptions);

            services.AddTransient<IStreamUploadHandler, StreamUploadHandler<TModule, TContext>>();

            return services;
        }
    }
}
