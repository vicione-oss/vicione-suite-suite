using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;

namespace Blazor.Shared.UserManagement.Services;

public class ExternalAccountService(UserManager<SuiteUser> userManager) : IExternalAccountService
{
    public async Task<ExternalUserAccount?> GetExternalUserAccount(string userName)
    {
        var user = await userManager.FindByNameAsync(userName);
        if (user == null)
            return null;

        var userLoginInfos = await userManager.GetLoginsAsync(user);

        return userLoginInfos
            .Select(info => new ExternalUserAccount(info.LoginProvider, info.ProviderDisplayName, info.ProviderKey))
            .SingleOrDefault();
    }
}
