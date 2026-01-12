namespace Core.OS.Modules.Services;

/// <summary>
/// https://learn.microsoft.com/en-us/dotnet/core/extensions/custom-configuration-provider
/// </summary>
/// <param name="moduleHost"></param>
/// <param name="optionsStore"></param>
public sealed class ModuleOptionsSource(IModuleHost moduleHost, IModuleOptionsStore optionsStore) : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        new ModuleOptionsProvider(moduleHost, optionsStore);
}
