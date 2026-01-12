using Sdk.Modules;
using Serilog;

namespace Core.OS.Modules.Services;

internal class ModuleOptionsProvider(IModuleHost moduleHost, IModuleOptionsStore optionsStore) : ConfigurationProvider
{
    public override void Load()
    {
        Data = moduleHost.GetModules()
            .Where(m => m.ModuleKey.ModuleType == ModuleType.Backend)
            .SelectMany(k => TryLoadOptions(k.ModuleKey.ModuleId))
            .ToDictionary();

        Dictionary<string, string?> TryLoadOptions(string moduleId)
        {
            try
            {
                return optionsStore.LoadDictionarySync(moduleId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load module options of '{ModuleId}'", moduleId);
            }
            return [];
        }
    }
}
