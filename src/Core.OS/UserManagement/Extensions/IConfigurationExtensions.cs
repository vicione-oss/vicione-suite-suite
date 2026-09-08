using Core.OS.UserManagement.Configuration;

namespace Core.OS.UserManagement.Extensions;

internal static class IConfigurationExtensions
{
    internal static UserManagementOptions GetUserManagementOptions(this IConfiguration config)
        => config.GetSection(UserManagementOptions.ConfigSection).Get<UserManagementOptions>() ?? new();
}
