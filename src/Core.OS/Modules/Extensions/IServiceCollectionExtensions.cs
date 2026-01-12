using System.IO.Abstractions;
using Core.Module;
using Core.Module.JFrog;
using Core.Module.Options;
using Core.OS.Instance;
using Core.OS.Modules.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Sdk.Backend.ArtifactApi;
using Sdk.Backend.Modules;

namespace Core.OS.Modules.Extensions;

internal static class IServiceCollectionExtensions
{
    public static async Task<IModuleManifestProvider> AddModuleManifestProvider(this IServiceCollection services, IFileSystem fileSystem, InstanceOptions instanceOptions, Serilog.ILogger logger, CancellationToken cancellationToken = default)
    {
        var manifestProvider = new ModuleManifestProvider(fileSystem, instanceOptions);
        await manifestProvider.LoadPackageManifest(logger, cancellationToken);

        services.AddSingleton<IModuleManifestProvider>(manifestProvider);

        return manifestProvider;
    }

    public static IServiceCollection AddModuleServices(this IServiceCollection services)
    {
        services.AddModuleArtifactQueryApi();
        services.AddWorkspaceManagement();

        services.AddSingleton<IModuleMetadataCache, ModuleMetadataCache>();
        services.AddSingleton<IModuleMigrator, ModuleMigrator>();

        return services;
    }

    public static IServiceCollection AddModuleArtifactQueryApi(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddTransient<IArtifactQueryApi, JFrogArtifactQueryApi>();
        services.AddTransient<ModuleApiAdapter>();
        services.AddTransient<IModuleApiAdapter>(s =>
        {
            var opt = s.GetRequiredService<IOptions<ModuleApiOptions>>();
            if (opt.Value.PackageApi == PackageApi.Nexus)
                throw new NotSupportedException("Nexus api need to be reworked");

            return s.GetRequiredService<ModuleApiAdapter>();
        });

        return services;
    }

    private static IServiceCollection AddWorkspaceManagement(this IServiceCollection services)
    {
        return services
            .AddWorkspaceProvider(typeof(SystemBackendModule))
            .AddSingleton<IWorkspaceManagement, WorkspaceManagement>();
    }

    public static IServiceCollection AddWorkspaceProvider(this IServiceCollection services, Type moduleType)
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
