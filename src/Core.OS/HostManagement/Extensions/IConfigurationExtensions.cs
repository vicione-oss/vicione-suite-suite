using Core.Shared.HostManagement;

namespace Core.OS.HostManagement.Extensions;

internal static class IConfigurationExtensions
{
    /// <summary>
    /// Bind section <see cref="HostManagementOptions.ConfigSection"/> to <see cref="HostManagementOptions"/>
    /// </summary>
    internal static HostManagementOptions GetHostManagementOptions(this IConfiguration config)
        => config.GetSection(HostManagementOptions.ConfigSection).Get<HostManagementOptions>()
           ?? new HostManagementOptions();
}
