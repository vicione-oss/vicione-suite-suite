using Sdk.Modules;

namespace Core.OS.Modules.Extensions;

internal static class IModuleOptionsStoreExtensions
{
    extension(IModuleOptionsStore optionsStore)
    {
        public async Task ApplyStoredOptions(ModuleMetadata metadata, IConfiguration configuration, CancellationToken cancellationToken)
        {
            // Stored options are merged in.
            // for now we only take options that are defined within metadata but custom ones need to be added soon
            if (metadata.Options is null || metadata.Options.Count == 0)
                return;

            // The json options are expected to exist.
            var options = await optionsStore.LoadJsonDictionary(metadata.Name, cancellationToken);

            foreach (var option in metadata.Options)
            {
                var optionKey = option.GetOptionKey(metadata.Name);

                // The environment is checked first, because it overrides the json settings.
                var envValue = configuration.GetValue<string?>(optionKey);
                if (!string.IsNullOrWhiteSpace(envValue))
                {
                    option.Value = ModuleConstants.SetByEnvironmentMarker;
                    continue;
                }

                // A matching key in the json config.
                if (options.TryGetValue(optionKey, out var value))
                {
                    // The metadata default cannot be used here, because it is not part of the
                    // effective configuration
                    option.Value = value;
                }
            }
        }
    }
}
