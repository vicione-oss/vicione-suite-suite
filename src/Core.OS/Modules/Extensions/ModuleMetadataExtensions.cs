using Sdk.Modules;

namespace Core.OS.Modules.Extensions;

internal static class ModuleMetadataExtensions
{
    public static Dictionary<string, string> GetConfigurationOptions(this ModuleMetadata metadata, bool requiredOnly = false)
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

    public static List<string> GetMissingConfigurationKeys(this ModuleMetadata metadata, IConfiguration envConf, bool requiredOnly = false)
    {
        if (metadata.Options is null)
            return [];

        var result = new List<string>();

        foreach (var declaration in metadata.Options)
        {
            if (requiredOnly && !declaration.IsRequired)
                continue;

            var optionKey = declaration.GetOptionKey(metadata.Name);
            var optionValue = envConf[optionKey];

            // good - it has a value - unvalidated
            if (!string.IsNullOrWhiteSpace(optionValue) || !string.IsNullOrEmpty(declaration.DefaultValue))
                continue;

            result.Add(declaration.Key);
        }

        return result;
    }

    public static List<string> ValidateAndUseDefaultValues(this ModuleMetadata metadata, IConfiguration envConf)
    {
        if (metadata.Options is null)
            return [];

        var result = new List<string>();

        foreach (var declaration in metadata.Options)
        {
            var optionKey = declaration.GetOptionKey(metadata.Name);
            var optionValue = envConf[optionKey];

            // good - we have something
            if (!string.IsNullOrWhiteSpace(optionValue))
                continue;

            // settings were overriden in module_settings.json or env vars
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
