using Core.Module.Options;
using Core.Shared;
using Microsoft.Extensions.Configuration;

namespace Core.Module.Tests;

public static class IConfigurationExtensions
{
    public static ModuleLoaderOptions GetTestModuleLoaderOptions(this IConfiguration config)
        => config.GetSection(Sdk.Constants.ModuleLoaderSection).Get<ModuleLoaderOptions>() ??
            throw new ConfigurationException(Sdk.Constants.ModuleLoaderSection);
}
