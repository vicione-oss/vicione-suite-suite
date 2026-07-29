using System.Reflection;
using Sdk.Backend.Modules;
using Sdk.Modules;

namespace Core.UiHosting;

public interface IUiHostEnvironment
{
    bool IsDevelopment { get; }

    string? ModulePath { get; }

    string? GetWwwRootFolder();

    BackendModule? GetBackendModule(string moduleId);

    Dictionary<ModulePathInfo, string> GetModulesContentPathInfos();

    IEnumerable<ModuleMetadata> GetModuleMetadata();

    IEnumerable<IUiModuleBundle> LoadModuleBundles(Func<string, Assembly, IUiModuleBundle?> createBundle);
}
