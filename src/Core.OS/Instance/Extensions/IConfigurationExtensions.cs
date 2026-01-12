using ConfigurationException = Core.Shared.ConfigurationException;

namespace Core.OS.Instance.Extensions;

internal static class IConfigurationExtensions
{
    internal static InstanceOptions GetInstanceOptions(this IConfiguration config)
        => config.GetSection(InstanceOptions.ConfigSection).Get<InstanceOptions>()
            ?? throw new ConfigurationException(InstanceOptions.ConfigSection);
}
