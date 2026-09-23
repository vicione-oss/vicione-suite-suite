using Core.Module.Contracts;
using Core.Module.Options;
using Core.Shared;
using Sdk.Backend.Extensions;
using Sdk.Modules;

namespace Core.OS.Modules.Extensions;

internal static class IConfigurationExtensions
{
    extension(IConfiguration config)
    {
        /// <summary>
        /// Bind section <see cref="Sdk.Constants.ModuleLoaderSection"/> to <see cref="ModuleLoaderOptions"/>
        /// </summary>
        internal ModuleLoaderOptions GetModuleLoaderOptions()
            => config.GetSection(ModuleLoaderOptions.ConfigSection).Get<ModuleLoaderOptions>() ??
                throw new ConfigurationException(ModuleLoaderOptions.ConfigSection);

        internal UiHostOptions? CreateUiHostOptions(ModuleLoaderOptions options)
        {
            if (string.IsNullOrEmpty(options.UiHost))
                return null;

            return config.BindSection<UiHostOptions>(options.UiHost);
        }

        /// <summary>
        /// All modules provided by the <see cref="ModulePackageManifest"/> are enabled per default except their <see cref="ModuleOptions.Enable"/> flag is overriden
        /// in the settings. The same way modules from the debug manifest are treated
        /// </summary>
        /// <returns>{{ModuleId, ModuleOptions}, ..}</returns>
        internal Dictionary<string, ModuleOptions> CreateModuleOptions(ModulePackageManifest manifest, params string[] additionalModuleIds)
        {
            var results = new Dictionary<string, ModuleOptions>();
            var packageNames = manifest.Packages
                .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                .Select(p => p.Name)
                .Union(additionalModuleIds); // for the samples modules

            // Modules in the manifest are enabled by default.
            foreach (var packageName in packageNames)
            {
                var moduleOptions = new ModuleOptions();

                // Config can disable one, so the section is checked.
                var section = config.GetSection(packageName);
                if (section.Value != null)
                {
                    section.Bind(moduleOptions);
                    continue;
                }

                // Binds the section when present.
                var val = section.Get<ModuleOptions>();
                if (val != null)
                {
                    results[packageName] = val;
                    continue;
                }

                // No overrides, so the module stays enabled.
                results[packageName] = moduleOptions;
            }

            return results;
        }
    }
}
