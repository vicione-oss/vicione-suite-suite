using Blazor.Shared.Module.Models;
using Core.Shared.Modules.Contracts;
using Sdk.Modules;

namespace Blazor.Shared.Module;

internal static class ModuleMetadataModelFactory
{
    public static List<ModuleMetadataModel> CreateModels(List<ModuleMetadataBundle> bundles)
        => bundles.Select(CreateModel).ToList();

    public static ModuleMetadataModel CreateModel(ModuleMetadataBundle bundle)
    {
        return new ModuleMetadataModel
        {
            Bundle = bundle,
            Installed = bundle.Installed,
            EditOptions = GetOptionDeclarations(bundle),
            SelectedVersion = bundle.AvailableVersions.FirstOrDefault(),
        };

        static Dictionary<string, ModuleOptionDeclaration> GetOptionDeclarations(ModuleMetadataBundle bundle)
        {
            if (bundle.Metadata.Options is null)
                return [];

            // options for installed modules are already enriched with current values
            if (!bundle.Installed)
            {
                // available modules have the options directly from metadata...
                foreach (var option in bundle.Metadata.Options
                    .Where(option => string.IsNullOrEmpty(option.Value) && !string.IsNullOrEmpty(option.DefaultValue)))
                {
                    // therefore we preset the default values here
                    option.Value = option.DefaultValue;
                }
            }

            return bundle.Metadata.Options.OrderBy(k => k.Key).ToDictionary(o => o.Key);
        }
    }
}
