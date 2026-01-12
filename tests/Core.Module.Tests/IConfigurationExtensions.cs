using Core.Module.Options;
using Core.Shared;
using Microsoft.Extensions.Configuration;

namespace Core.Module.Tests;

public static class IConfigurationExtensions
{
    public static ModuleLoaderOptions GetModuleLoaderTestOptions(this IConfiguration config)
        => config.GetSection(ModuleLoaderOptions.ConfigSection).Get<ModuleLoaderOptions>() ??
            throw new ConfigurationException(ModuleLoaderOptions.ConfigSection);
}
