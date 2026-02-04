using System.IO.Abstractions;
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
        /// <summary>
        /// Creates an instance of <see cref="ModuleManifestProvider"/>, adds it to service collection and returns it.
        /// It's used before IServiceProvider is available therefore the instance gets directly used.
        /// </summary>    
        public async Task<IModuleManifestProvider> AddModuleManifestProvider(IFileSystem fileSystem, InstanceOptions instanceOptions, Serilog.ILogger logger, CancellationToken cancellationToken = default)
        {
            var manifestProvider = new ModuleManifestProvider(fileSystem, instanceOptions);
            await manifestProvider.LoadPackageManifest(logger, cancellationToken);

            services.AddSingleton<IModuleManifestProvider>(manifestProvider);

            return manifestProvider;
        }

        public IServiceCollection AddModuleServices()
        {
            services.AddModuleArtifactQueryApi();
            services.AddWorkspaceManagement();

            services.AddSingleton<IModuleMetadataCache, ModuleMetadataCache>();
            services.AddSingleton<IModuleMigrator, ModuleMigrator>();

            return services;
        }

        private IServiceCollection AddModuleArtifactQueryApi()
        {
            services.AddHttpClient();
            services.AddTransient<JFrogArtifactRepository>();
            services.AddTransient<IArtifactRepository, JFrogArtifactRepository>();
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
