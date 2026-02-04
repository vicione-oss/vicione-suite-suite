using Core.Module.Contracts;
using Core.Module.Options;
using Core.Shared;
using Sdk.Backend.Extensions;
using Sdk.Modules;

namespace Core.OS.Modules.Extensions;

internal static class IConfigurationExtensions
{
    /// <summary>
    /// Bind section <see cref="Sdk.Constants.ModuleLoaderSection"/> to <see cref="ModuleLoaderOptions"/>
    /// </summary>    
    internal static ModuleLoaderOptions GetModuleLoaderOptions(this IConfiguration config)
        => config.GetSection(ModuleLoaderOptions.ConfigSection).Get<ModuleLoaderOptions>() ??
            throw new ConfigurationException(ModuleLoaderOptions.ConfigSection);

    /// <summary>
    /// Bind section <see cref="ArtifactRepositoryOptions.ConfigSection"/> to <see cref="ArtifactRepositoryOptions"/>
    /// </summary>
    internal static ArtifactRepositoryOptions GetArtifactRepositoryOptions(this IConfiguration config)
    {
        var options = config.GetSection(ArtifactRepositoryOptions.ConfigSection).Get<ArtifactRepositoryOptions>();

        // ModuleApiOptions will get removed once but to stay backwards compatible we
        // need to integrate it into the new ArtifactRepositoryOptions
#pragma warning disable CS0618 // Obsolete class usage
        var obsoleteOptions = config.GetSection(ModuleApiOptions.ConfigSection).Get<ModuleApiOptions>();
#pragma warning restore CS0618 // Obsolete class usage
        if (obsoleteOptions is not null)
        {
            options ??= new ArtifactRepositoryOptions();

            if (options.Sources.All(k => k.Endpoint != obsoleteOptions.Endpoint))
            {
                options.Sources.Add(new ArtifactRepositorySource
                {
                    Endpoint = obsoleteOptions.Endpoint,
                    UserName = obsoleteOptions.UserName,
                    Password = obsoleteOptions.Password,
                });
            }

            options.PackageCacheLifetimeMs = obsoleteOptions.PackageCacheLifetimeMs;

            return options;
        }

        return options ?? throw new ConfigurationException(ArtifactRepositoryOptions.ConfigSection);
    }

    extension(IConfiguration config)
    {
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
        internal Dictionary<string, ModuleOptions> CreateModuleOptions(IModuleManifestProvider manifestProvider, params string[] additionalModuleIds)
        {
            var results = new Dictionary<string, ModuleOptions>();
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
}
