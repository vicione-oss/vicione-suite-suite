using System.Reflection;
using Sdk.Modules;

namespace Core.UiHosting;

public interface IUiHostEnvironment
{
    bool IsDevelopment { get; }

    string? ModulePath { get; }

    string? GetWwwRootFolder();

    Dictionary<ModulePathInfo, string> GetModulesContentPathInfos();

    IEnumerable<ModuleMetadata> GetModuleMetadata();

    IEnumerable<IUiModuleBundle> LoadModuleBundles(Func<string, Assembly, IUiModuleBundle?> createBundle);
}
