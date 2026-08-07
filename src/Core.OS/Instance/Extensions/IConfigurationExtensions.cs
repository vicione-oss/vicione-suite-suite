using ConfigurationException = Core.Shared.ConfigurationException;

namespace Core.OS.Instance.Extensions;

internal static class IConfigurationExtensions
{
    internal static InstanceOptions GetInstanceOptions(this IConfiguration config)
        => config.GetSection(InstanceOptions.ConfigSection).Get<InstanceOptions>()
            ?? throw new ConfigurationException(InstanceOptions.ConfigSection);

    /// <summary>
    /// The home directory alone, for callers that run before the options pipeline exists and
    /// cannot bind - and validate - the whole section yet.
    /// </summary>
    internal static string? GetInstanceHomeDirectory(this IConfiguration config)
        => config[$"{InstanceOptions.ConfigSection}:{nameof(InstanceOptions.HomeDirectory)}"];
}
