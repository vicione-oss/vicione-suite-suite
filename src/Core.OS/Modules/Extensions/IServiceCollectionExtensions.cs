using Core.Artifacts.Extensions;
using Core.Module;
using Core.OS.Instance;
using Core.OS.Modules.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sdk.Backend.Modules;

namespace Core.OS.Modules.Extensions;

internal static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddModuleServices(IModuleHost moduleHost, IModuleOptionsStore optionStore)
        {
            services
                .AddWorkspaceManagement()
                .AddSingleton(moduleHost)
                .AddSingleton(optionStore)
                .AddSingleton<IModuleArtifactCache, ModuleArtifactCache>()
                .AddTransient<IModuleMetadataProvider, ModuleMetadataProvider>()
                .AddSingleton<IModulePackageManifestStore, ModulePackageManifestStore>()
                .AddSingleton<IModulePackageOperationStore, ModulePackageOperationStore>();

            return services;
        }

        /// <summary>
        /// Hosted services that drive module lifecycle. Registered after the message bus so
        /// <see cref="ApplicationWorker"/> starts once the bus is ready.
        /// </summary>
        internal IServiceCollection AddModuleHosting()
        {
#if DEBUG
            services.AddHostedService<ApplicationPartsLogger>();
#endif
            services.AddHostedService<ApplicationWorker>();

            return services;
        }

        public IServiceCollection AddModuleArtifactQueryApi(IArtifactRepositoryOptionsCache optionsCache)
        {
            services.AddSingleton(optionsCache);
            services.AddArtifactRepository(s => s.GetRequiredService<IArtifactRepositoryOptionsCache>());
            services.AddSingleton<IArtifactPlatform>(ArtifactPlatform.Current);
            services.AddTransient<IModuleArtifactRepository, ModuleArtifactRepository>();
            services.AddTransient<ISuiteArtifactRepository, SuiteArtifactRepository>();

            return services;
        }

        private IServiceCollection AddWorkspaceManagement()
        {
            return services
                .AddWorkspaceProvider(typeof(SystemBackendModule))
                .AddSingleton<IWorkspaceManagement, WorkspaceManagement>();
        }

        public IServiceCollection AddWorkspaceProvider(Type moduleType)
        {
            if (!moduleType.IsAssignableTo(typeof(BackendModule)))
                throw new ArgumentOutOfRangeException(nameof(moduleType));

            var workspaceProvider = new ServiceDescriptor(
                typeof(IWorkspaceProvider<>).MakeGenericType(moduleType),
                typeof(WorkspaceProvider<>).MakeGenericType(moduleType),
                ServiceLifetime.Transient);
            services.TryAdd(workspaceProvider);
            return services;
        }
    }
}
