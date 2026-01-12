using Core.Shared.UserManagement.Configuration;
using Microsoft.Extensions.Options;

namespace Core.OS.UserManagement.Configuration;

internal sealed class AdministratorNameProvider(IOptions<UserManagementOptions> userManagementOptions) : IAdministratorNameProvider
{
    public string GetAdministratorName()
        => userManagementOptions.Value.AdministratorName;
}
