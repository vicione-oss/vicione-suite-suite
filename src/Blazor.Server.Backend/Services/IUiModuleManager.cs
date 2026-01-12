using System.Reflection;
using Sdk.Client.Modules;

namespace Blazor.Server.Backend.Services;

public interface IUiModuleManager
{
    IEnumerable<ClientModule> UiModules { get; }
    IEnumerable<Assembly> UiModuleAssemblies { get; }

    Assembly[] GetAdditionalAssemblies();
}
