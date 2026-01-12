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
    /// <summary>
    /// Creates an instance of <see cref="ModuleManifestProvider"/>, adds it to service collection and returns it.
    /// It's used before IServiceProvider is available therefore the instance gets directly used.
    /// </summary>    
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
        services.AddTransient<JFrogArtifactRepository>();
        services.AddTransient<IArtifactRepository, JFrogArtifactRepository>();
        services.AddTransient<IModuleArtifactRepository, ModuleArtifactRepository>();
        services.AddTransient<ISuiteArtifactRepository, SuiteArtifactRepository>();

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
