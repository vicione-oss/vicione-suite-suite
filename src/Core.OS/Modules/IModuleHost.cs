using System.Reflection;
using Core.Module;
using Core.Shared.Modules.Contracts;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Sdk.Backend.Modules;
using Sdk.Modules;

namespace Core.OS.Modules;

public interface IModuleHost
{
    void AddModuleServices(IServiceCollection services, IMvcBuilder? mvcBuilder = null);

    void AddUiHostServices(IServiceCollection services, Func<IServiceCollection, IdentityBuilder?> getIdentity);

    void AddUiHostServices(IServiceCollection services, IMvcBuilder mvcBuilder, Func<IServiceCollection, IdentityBuilder?> getIdentity);

    Task CallOnInitialized(IServiceScope scope, CancellationToken stoppingToken);

    void ConfigureBusRegistrationConfigurator(IBusRegistrationConfigurator busConfig);

    SuiteDependencyContext GetContext();

    T? GetModule<T>() where T : BackendModule;

    IEnumerable<IModule> GetModules();

    IEnumerable<Assembly> GetModuleAssemblies();

    IReadOnlyCollection<ModuleMetadataBundle> GetManifestModules();

    string GetSdkVersion();

    void MapModuleEndpoints(IEndpointRouteBuilder endpoints);

    Task MigrateAndSeedModuleData(IServiceScope scope, IConfiguration config, CancellationToken stoppingToken);

    void MoveModuleResources(IServiceProvider serviceProvider);

    void UseModuleServices(IApplicationBuilder app);

    void UseSecurity(IApplicationBuilder app);

    void UseUiHost(IApplicationBuilder app, IWebHostEnvironment env);
}
