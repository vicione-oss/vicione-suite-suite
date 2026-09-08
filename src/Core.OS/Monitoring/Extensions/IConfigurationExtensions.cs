using Core.Shared.Monitoring;

namespace Core.OS.Monitoring.Extensions;

internal static class IConfigurationExtensions
{
    /// <summary>
    /// System monitoring is off unless the section is present, so an absent section
    /// binds to <c>null</c> rather than a defaulted instance.
    /// </summary>
    internal static SystemMonitoringOptions? GetSystemMonitoringOptions(this IConfiguration config)
        => config.GetSection(SystemMonitoringOptions.ConfigSection).Get<SystemMonitoringOptions>();
}
