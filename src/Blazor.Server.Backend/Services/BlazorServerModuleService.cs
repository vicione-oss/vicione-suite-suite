using System.Reflection;
using System.Security.Claims;
using Blazor.Shared.Extensions;
using Blazor.Shared.Module.Services;
using Core.UiHosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Client.Modules;
using Sdk.Modules;

namespace Blazor.Server.Backend.Services;

public sealed class BlazorServerModuleService(IUiModuleManager uiModuleManager, IUiHostEnvironment hostEnvironment) : IClientModuleService
{
    private List<ClientModule> Modules => [.. uiModuleManager.UiModules];

    public IEnumerable<ModuleMetadata> GetModuleMetadata()
        => hostEnvironment.GetModuleMetadata();

    public List<ClientModule> GetModules()
        => [.. Modules];

    public ClientModule? GetModuleByType(Type moduleType)
        => Modules.FirstOrDefault(k => k.GetType() == moduleType);

    public IEnumerable<Assembly> GetModuleAssemblies()
        => uiModuleManager.GetAdditionalAssemblies();

    public IEnumerable<string> GetAllModuleStylesheets()
    {
        // Blazor.Server.Backend also contains the client module so it can register frontend services,
        // but _Host.cshtml references its stylesheet directly, so it is filtered out here.
        var serverAssembly = typeof(BlazorServerBackendModule).Assembly;

        return (from assembly in uiModuleManager.UiModuleAssemblies
                select assembly.GetName()
            into assemblyName
                where assemblyName.Name is not null && assemblyName.Name != serverAssembly.GetName().Name
                select GetRelativeModuleBundleStylesheetPath(assemblyName.Name!)).ToList();
    }

    /// <summary>
    /// Called for every browser tab connection. Calls <see cref="ClientModule.InitializeServices"/> for
    /// each loaded client module and logs any error without failing the connection.
    /// </summary>
    public async Task InitializeServices(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<BlazorServerModuleService>>();

        serviceProvider.UseSharedServices();

        try
        {

            await serviceProvider.InitializeSharedServices();
        }
        catch (ObjectDisposedException)
        {
            // The circuit is already gone; there is nothing left to initialize.
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to initialize shared services");
        }

        foreach (var clientModule in Modules)
        {
            try
            {
                if (clientModule.InitializeServices is not null)
                    await clientModule.InitializeServices.Invoke(serviceProvider);
            }
            catch (Exception e)
            {
                logger.LogError(e, "{MethodName} {ClientModule} failed", nameof(ClientModule.InitializeServices), clientModule);
            }
        }
    }

    public async Task OnUserAuthenticated(IServiceProvider serviceProvider, ClaimsPrincipal user)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<BlazorServerModuleService>>();
        foreach (var clientModule in Modules)
        {
            try
            {
                if (clientModule.OnUserAuthenticated is not null)
                    await clientModule.OnUserAuthenticated.Invoke(serviceProvider, user);
            }
            catch (Exception e)
            {
                logger.LogError(e, "{MethodName} {ClientModule} failed", nameof(ClientModule.OnUserAuthenticated), clientModule);
            }
        }
    }

    /// <summary>
    /// Returns /_content/Assembly.Name/Assembly.Name.styles.css.
    /// </summary>
    private static string GetRelativeModuleBundleStylesheetPath(string assemblyName)
        => $"/{ModuleAssetHelper.ContentPrefix}/{assemblyName}/{assemblyName}.styles.css";
}
