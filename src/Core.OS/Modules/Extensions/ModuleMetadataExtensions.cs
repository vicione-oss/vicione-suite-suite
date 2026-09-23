using Sdk.Modules;

namespace Core.OS.Modules.Extensions;

internal static class ModuleMetadataExtensions
{
    extension(ModuleMetadata metadata)
    {
        public Dictionary<string, string> GetConfigurationOptions(bool requiredOnly = false)
        {
            var result = new Dictionary<string, string>();

            if (metadata.Options is null)
                return result;

            foreach (var declaration in metadata.Options)
            {
                if (requiredOnly && !declaration.IsRequired)
                    continue;

                result.Add(declaration.GetOptionKey(metadata.Name), declaration.Value ?? string.Empty);
            }

            return result;
        }

        public List<string> GetMissingConfigurationKeys(IConfiguration configuration, bool requiredOnly = false)
        {
            if (metadata.Options is null)
                return [];

            var result = new List<string>();

            foreach (var declaration in metadata.Options)
            {
                if (requiredOnly && !declaration.IsRequired)
                    continue;

                var optionKey = declaration.GetOptionKey(metadata.Name);
                var optionValue = configuration[optionKey];

                GetConfigurationOption(declaration, optionKey, configuration);

                // A value or a default is present; validation happens elsewhere.
                if (!string.IsNullOrWhiteSpace(optionValue) || !string.IsNullOrEmpty(declaration.DefaultValue))
                    continue;

                result.Add(declaration.Key);
            }

            return result;
        }

        /// <summary>
        /// Attempt to get the option value from configuration will throw if the option
        /// is totally misconfigured and will result in StartupError of the module
        /// </summary>
        private static void GetConfigurationOption(ModuleOptionDeclaration declaration, string optionKey, IConfiguration configuration)
        {
            switch (declaration.OptionType)
            {
                case ModuleOptionType.Number:
                    configuration.GetValue<int>(optionKey);
                    break;
                case ModuleOptionType.Text:
                    configuration.GetValue<string>(optionKey);
                    break;
                case ModuleOptionType.Boolean:
                    configuration.GetValue<bool>(optionKey);
                    break;
            }
        }

        public List<string> ValidateAndUseDefaultValues(IConfiguration envConf)
        {
            if (metadata.Options is null)
                return [];

            var result = new List<string>();

            foreach (var declaration in metadata.Options)
            {
                var optionKey = declaration.GetOptionKey(metadata.Name);
                var optionValue = envConf[optionKey];

                // A value is present.
                if (!string.IsNullOrWhiteSpace(optionValue))
                    continue;

                // Overridden in module_settings.json or by env vars.
                // we should fail here
                if (!string.IsNullOrEmpty(declaration.DefaultValue))
                {
                    declaration.Value = optionValue;
                    continue;
                }

                // todo - we really need to check what the DataAnnotations require
                if (declaration.IsRequired)
                    result.Add(declaration.Key);
            }

            return result;
        }
    }
}
