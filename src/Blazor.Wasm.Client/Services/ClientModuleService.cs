using System.Reflection;
using System.Security.Claims;
using Blazor.Wasm.Client.Extensions;
using Core.Shared.Extensions;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Sdk.Client.Modules;
using Sdk.Client.Services;
using Sdk.Extensions;
using Sdk.Modules;

namespace Blazor.Wasm.Client.Services;

public sealed class ClientModuleService : IClientModuleService
{
    private static List<Assembly>? _assemblies;
    private static List<ClientModule>? _modules;
    private static List<ModuleMetadata>? _moduleInfos;

    public static List<Assembly> ModuleAssemblies => _assemblies ??= GetModuleAssembliesInternal();

    private static List<ClientModule> Modules => _modules ??= GetClientModules();

    private static List<ModuleMetadata> ModuleInfos => _moduleInfos ??= [];

    public List<ClientModule> GetModules() => new(Modules);

    public static ClientModule? GetModuleByType(Type moduleType) => Modules.FirstOrDefault(k => k.GetType() == moduleType);

    internal static void SetBackendModuleInfos(IEnumerable<ModuleMetadata> moduleInfos)
    {
        ModuleInfos.Clear();
        ModuleInfos.AddRange(moduleInfos);
    }

    private static List<ClientModule> GetClientModules()
    {
        // get client modules from other assemblies
        var external = ModuleAssemblies.GetInstances<ClientModule>().ToList();

        // get client modules from client assembly
        var clientAssembly = Assembly.GetEntryAssembly();
        if (clientAssembly is not null)
            external.AddRange(clientAssembly.GetInstances<ClientModule>());

        return external;
    }

    public IEnumerable<Assembly> GetModuleAssemblies() => GetModuleAssembliesInternal();

    private static List<Assembly> GetModuleAssembliesInternal()
    {
        var fullName = typeof(ClientModule).FullName;
        var clientAssembly = Assembly.GetEntryAssembly();

        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.IsSuiteModule(fullName!) && !Equals(a.FullName, clientAssembly?.FullName))
            .ToList();
    }

    public static void RegisterModuleServices(WebAssemblyHostBuilder builder)
    {
        Console.WriteLine($"Configuring services (env:{builder.HostEnvironment.Environment})");

        foreach (var module in Modules)
        {
            try
            {
                var moduleServices = new ServiceCollection();
                module.ConfigureServices?.Invoke(moduleServices, HostingModel.BlazorWasm);

                foreach (var service in moduleServices)
                    builder.Services.Add(service);
            }
            catch (Exception e)
            {
                builder.HostEnvironment.LogInDevelopment($"Failed to configure services for module {module.ModuleId} - {e.Message}");
            }
        }
    }

    public async Task InitializeServices(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<ClientModuleService>>();
        foreach (var clientModule in Modules)
        {
            try
            {
                if (clientModule.InitializeServices is not null)
                    await clientModule.InitializeServices.Invoke(serviceProvider);
            }
            catch (Exception e)
            {
                logger.LogError(e, "{NameOfInitializeServices} module {ModuleId}", nameof(InitializeServices), clientModule.ModuleId);
            }
        }
    }

    public async Task OnUserAuthenticated(IServiceProvider serviceProvider, ClaimsPrincipal user)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<ClientModuleService>>();
        foreach (var clientModule in Modules)
        {
            try
            {
                if (clientModule.OnUserAuthenticated is not null)
                    await clientModule.OnUserAuthenticated.Invoke(serviceProvider, user);
            }
            catch (Exception e)
            {
                logger.LogError(e, "{NameOfOnUserAuthenticated} module {ModuleId}", nameof(OnUserAuthenticated), clientModule.ModuleId);
            }
        }
    }

    private static List<string> GetAllModuleStylesheetsStatic() =>
        (from assembly in ModuleAssemblies
         select assembly.GetName()
            into assemblyName
         where assemblyName.Name is not null
         select GetRelativeModuleBundleStylesheetPath(assemblyName.Name!)).ToList();

    public IEnumerable<string> GetAllModuleStylesheets() => GetAllModuleStylesheetsStatic();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="assemblyName"></param>
    /// <returns>/_content/Assembly.Name/Assembly.Name.styles.css</returns>
    private static string GetRelativeModuleBundleStylesheetPath(string assemblyName)
        => $"/{ModuleAssetHelper.ContentPrefix}/{assemblyName}/{assemblyName}.styles.css";
}
