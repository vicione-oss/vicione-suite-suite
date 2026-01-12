using Core.Module;
using Core.Module.Options;
using Microsoft.Extensions.Configuration;

namespace Suite.Deps;

internal static class ConfigurationUtils
{
    private const string SuiteCoreOsDepsJson = "ViciOne.Suite.Core.OS.deps.json";

    public static string GetCoreDepsJsonFilePath(string suitePath)
    {
        var coreDepsJsonFile = Path.Combine(suitePath, SuiteCoreOsDepsJson);

        return File.Exists(coreDepsJsonFile)
            ? coreDepsJsonFile
            : throw new FileNotFoundException($"core assembly not found in {coreDepsJsonFile}");
    }

    public static IConfiguration GetSuiteConfiguration(string suitePath, string? environment = null)
    {
        var appsettingsFile = Path.Combine(suitePath, "appsettings.json");
        if (!File.Exists(appsettingsFile))
            throw new FileNotFoundException($"suite {appsettingsFile} not found");

        var configBuilder = new ConfigurationBuilder()
            .AddJsonFile(appsettingsFile);

        if (!string.IsNullOrEmpty(environment))
        {
            var envsettingsFile = Path.Combine(suitePath, $"appsettings.{environment}.json");
            if (!File.Exists(envsettingsFile))
                throw new FileNotFoundException($"suite {envsettingsFile} not found");

            configBuilder.AddJsonFile(envsettingsFile);
        }

        return configBuilder.Build();
    }

    public static ModuleLoaderOptions GetSuiteModuleLoaderOptions(IConfiguration config, string suitePath)
    {
        var options = config.GetSection(ModuleLoaderOptions.ConfigSection).Get<ModuleLoaderOptions>() ??
            new ModuleLoaderOptions
            {
                ModulesPath = "Modules",
                UiHost = Constants.BlazorServerModuleId,
            };

        // we have to set it rooted because we do it from another workspace
        options.ModulesPath = Path.Combine(suitePath, options.ModulesPath ?? "Modules");

        if (!string.IsNullOrEmpty(options.UiHostsPath) && !string.IsNullOrEmpty(options.UiHost))
            options.UiHostsPath = Path.Combine(suitePath, options.UiHostsPath, options.UiHost);

        return options;
    }

    public static UiHostOptions? GetUiHostOptions(this IConfiguration config, string uiHostModuleId)
        => config.GetSection(uiHostModuleId).Get<UiHostOptions>();
}
