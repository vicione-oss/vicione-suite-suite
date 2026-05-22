using Sdk.Modules;

namespace Core.OS.Modules.Extensions;

internal static class IModuleOptionsStoreExtensions
{
    extension(IModuleOptionsStore optionsStore)
    {
        public async Task ApplyStoredOptions(ModuleMetadata metadata, IConfiguration configuration, CancellationToken cancellationToken)
        {
            // enrich the options with already stored ones
            // for now we only take options that are defined within metadata but custom ones need to be added soon
            if (metadata.Options is null || metadata.Options.Count == 0)
                return;

            // json options - we expect it exists
            var options = await optionsStore.LoadJsonDictionary(metadata.Name, cancellationToken);

            foreach (var option in metadata.Options)
            {
                var optionKey = option.GetOptionKey(metadata.Name);

                // first check environment - it overrides json settings
                var envValue = configuration.GetValue<string?>(optionKey);
                if (!string.IsNullOrWhiteSpace(envValue))
                {
                    option.Value = ModuleConstants.SetByEnvironmentMarker;
                    continue;
                }

                // do we have matching key in json config?
                if (options.TryGetValue(optionKey, out var value))
                {
                    // we don't can take the default value of metadata here because it isn't part of the 
                    // effective configuration
                    option.Value = value;
                }
            }
        }
    }
}
