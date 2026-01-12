using Core.OS.Modules;
using Core.OS.Modules.Services;
using Microsoft.Extensions.Configuration.Json;


namespace Core.OS.Extensions;

internal static class IConfigurationBuilderExtensions
{
    internal static IConfigurationBuilder AddModuleConfigurationSource(this IConfigurationBuilder configurationBuilder, IModuleHost moduleHost, IModuleOptionsStore optionsStore)
    {
        // in some tests we don't use appsettings.json so it's optional
        var appsettingsSource = configurationBuilder.Sources.FirstOrDefault(k => k is JsonConfigurationSource { Path: "appsettings.json" });
        var appsettingsIndex = 2;
        if (appsettingsSource != null)
            appsettingsIndex = configurationBuilder.Sources.IndexOf(appsettingsSource);

        // add module options before appsettings so it should be overrideable
        configurationBuilder.Sources.Insert(appsettingsIndex, new ModuleOptionsSource(moduleHost, optionsStore));

        return configurationBuilder;
    }
}
