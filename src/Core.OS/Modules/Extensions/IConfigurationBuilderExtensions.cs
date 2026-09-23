using Core.OS.Modules.Services;
using Microsoft.Extensions.Configuration.Json;


namespace Core.OS.Modules.Extensions;

internal static class IConfigurationBuilderExtensions
{
    internal static IConfigurationBuilder AddModuleConfigurationSource(this IConfigurationBuilder configurationBuilder, IModuleHost moduleHost, IModuleOptionsStore optionsStore)
    {
        // Optional, because some tests run without appsettings.json.
        var appsettingsSource = configurationBuilder.Sources.FirstOrDefault(k => k is JsonConfigurationSource { Path: "appsettings.json" });
        var appsettingsIndex = 2;
        if (appsettingsSource != null)
            appsettingsIndex = configurationBuilder.Sources.IndexOf(appsettingsSource);

        // Module options come before appsettings so appsettings can override them.
        configurationBuilder.Sources.Insert(appsettingsIndex, new ModuleOptionsSource(moduleHost, optionsStore));

        return configurationBuilder;
    }
}
