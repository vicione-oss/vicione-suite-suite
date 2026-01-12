using Core.Module.Options;
using Core.Shared;
using Microsoft.Extensions.Configuration;

namespace Core.Module.Tests;

internal static class TestConfigExtensions
{
    internal static ModuleLoaderOptions GetModuleLoaderOptions(this IConfiguration config)
        => config.GetSection(Sdk.Constants.ModuleLoaderSection).Get<ModuleLoaderOptions>() ??
            throw new ConfigurationException(Sdk.Constants.ModuleLoaderSection);
}
