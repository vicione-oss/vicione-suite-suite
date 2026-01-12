using Core.Shared.UserManagement.Configuration;
using Microsoft.Extensions.Options;

namespace Core.OS.UserManagement.Configuration;

internal sealed class AdministratorInitialPasswordProvider(IOptions<UserManagementOptions> userManagementOptions) : IAdministratorInitialPasswordProvider
{
    public string GetAdministratorInitialPassword()
        => userManagementOptions.Value.InitialAdministratorPassword;
}
