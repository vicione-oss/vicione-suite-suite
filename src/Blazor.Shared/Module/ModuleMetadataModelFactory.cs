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

            // Installed modules already carry their current option values.
            if (!bundle.Installed)
            {
                // Available modules take their options straight from the metadata, so the defaults are
                foreach (var option in bundle.Metadata.Options
                    .Where(option => string.IsNullOrEmpty(option.Value) && !string.IsNullOrEmpty(option.DefaultValue)))
                {
                    // preset here.
                    option.Value = option.DefaultValue;
                }
            }

            return bundle.Metadata.Options.OrderBy(k => k.Key).ToDictionary(o => o.Key);
        }
    }
}
