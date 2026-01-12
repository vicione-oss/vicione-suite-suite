using Core.Module.Contracts;
using Core.Module.Options;
using Core.OS.Instance;
using Core.OS.MessageBus.MassTransit.Configuration;
using Core.OS.Modules;
using Core.Shared.HostManagement;
using MassTransit;
using Sdk.Backend.Extensions;
using Sdk.Modules;
using ConfigurationException = Core.Shared.ConfigurationException;


namespace Core.OS.Extensions;

internal static class IConfigurationExtensions
{
    internal static MessageBusOptions GetMessageBusOptions(this IConfiguration config)
    {
        var configItem = config.GetSection(MessageBusOptions.ConfigSection).Get<MessageBusOptions>() ?? new();
        configItem.Connection = config.GetSection(MessageBusOptions.TransportOptionsConfigSection).Get<RabbitMqTransportOptions>() ?? new();
        return configItem;
    }

    internal static ModuleLoaderOptions GetModuleLoaderOptions(this IConfiguration config)
        => config.GetSection(Sdk.Constants.ModuleLoaderSection).Get<ModuleLoaderOptions>() ??
            throw new ConfigurationException(Sdk.Constants.ModuleLoaderSection);

    internal static ModuleApiOptions GetModuleApiOptions(this IConfiguration config)
        => config.GetSection(Sdk.Constants.ModuleApiSection).Get<ModuleApiOptions>() ??
            throw new ConfigurationException(Sdk.Constants.ModuleApiSection);

    internal static InstanceOptions GetInstanceOptions(this IConfiguration config)
        => config.GetSection(InstanceOptions.ConfigSection).Get<InstanceOptions>()
            ?? throw new ConfigurationException(InstanceOptions.ConfigSection);

    internal static HostManagementOptions GetHostManagementOptions(this IConfiguration config)
        => config.GetSection(HostManagementOptions.ConfigSection).Get<HostManagementOptions>()
           ?? new HostManagementOptions();

    internal static Dictionary<string, ModuleOptions> CreateModuleOptions(this IConfiguration config, IModuleManifestProvider manifestProvider, ModuleLoaderOptions options)
        => config.CreateModuleOptions(manifestProvider, options, ModuleConstants.SampleModuleIds);

    /// <summary>
    /// All modules provided by the <see cref="ModulePackageManifest"/> are enabled per default except their <see cref="ModuleOptions.Enable"/> flag is overriden
    /// in the settings. The same way modules from the debug manifest are treated
    /// </summary>
    /// <param name="config"></param>
    /// <param name="manifestProvider"></param>
    /// <param name="options"></param>
    /// <param name="additionalModuleIds"></param>
    /// <returns></returns>
    internal static Dictionary<string, ModuleOptions> CreateModuleOptions(this IConfiguration config, IModuleManifestProvider manifestProvider, ModuleLoaderOptions options, params string[] additionalModuleIds)
    {
        var results = new Dictionary<string, ModuleOptions>();
        if (!string.IsNullOrEmpty(options.UiHost))
        {
            results[options.UiHost] = config.BindSection<UiHostOptions>(options.UiHost);
        }

        var manifest = manifestProvider.GetManifest();
        var packageNames = manifest.Packages
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .Select(p => p.Name)
            .Union(additionalModuleIds); // for the samples modules

        // by default modules in manifest are enabled
        foreach (var packageName in packageNames)
        {
            var moduleOptions = new ModuleOptions();

            // it can be disabled by config so check the section
            var section = config.GetSection(packageName);
            if (section.Value != null)
            {
                section.Bind(moduleOptions);
                continue;
            }

            // try to bind the section
            var val = section.Get<ModuleOptions>();
            if (val != null)
            {
                results[packageName] = val;
                continue;
            }

            // no overrides so module is enabled
            results[packageName] = moduleOptions;
        }

        return results;
    }
}
