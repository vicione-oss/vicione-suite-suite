using Core.Module;
using Core.Module.JFrog;
using Core.OS.Instance;
using Core.OS.Modules.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sdk.Backend.Artifacts;
using Sdk.Backend.Modules;

namespace Core.OS.Modules.Extensions;

internal static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddModuleServices()
        {
            services.AddModuleArtifactQueryApi();
            services.AddWorkspaceManagement();

            services
                .AddSingleton<IModuleArtifactCache, ModuleArtifactCache>()
                .AddTransient<IModuleMetadataProvider, ModuleMetadataProvider>()
                .AddSingleton<IModulePackageManifestStore, ModulePackageManifestStore>()
                .AddSingleton<IModulePackageOperationStore, ModulePackageOperationStore>();

            return services;
        }

        private IServiceCollection AddModuleArtifactQueryApi()
        {
            services.AddHttpClient();
            services.AddTransient<JFrogArtifactRepository>();
            services.AddTransient<IArtifactRepository, JFrogArtifactRepository>();
            services.AddSingleton<IArtifactRepositoryOptionsProvider>(s => s.GetRequiredService<IArtifactRepositoryOptionsCache>());
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
