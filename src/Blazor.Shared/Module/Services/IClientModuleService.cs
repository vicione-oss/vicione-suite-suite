using System.Reflection;
using System.Security.Claims;
using Sdk.Client.Modules;

namespace Blazor.Shared.Module.Services;

public interface IClientModuleService
{
    List<ClientModule> GetModules();

    IEnumerable<Assembly> GetModuleAssemblies();

    /// <summary>
    /// Stylesheet paths registered by the client modules.
    /// </summary>
    IEnumerable<string> GetAllModuleStylesheets();

    /// <summary>
    /// Initializes the services for all registered modules.
    /// </summary>
    Task InitializeServices(IServiceProvider serviceProvider);

    /// <summary>
    /// Notifies all registered modules that a user has been authenticated.
    /// </summary>
    Task OnUserAuthenticated(IServiceProvider serviceProvider, ClaimsPrincipal user);
}
