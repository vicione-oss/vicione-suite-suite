using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;

namespace Core.OS.UserManagement.Extensions;

internal static class UserManagerExtensions
{
    public static async Task InvalidateLogins(this UserManager<SuiteUser> userManager)
    {
        var users = userManager.Users.ToList();
        foreach (var user in users)
        {
            await userManager.UpdateSecurityStampAsync(user);
        }
    }

    public static async Task<bool> IsLastSystemAdministrator(this UserManager<SuiteUser> userManager, SuiteUser user)
    {
        var userRoles = (await userManager.GetRolesAsync(user)).Select(r => new Role(r));
        var sysAdminRoleName = SeedingExtensions.GetSystemAdministratorRoleName();
        var sysAdminRole = new Role(sysAdminRoleName);

        // is it an sys admin at all?
        if (!userRoles.Contains(sysAdminRole))
            return false;

        var sysAdmins = await userManager.GetUsersInRoleAsync(sysAdminRoleName);
        if (sysAdmins.Count == 0)
            throw new InvalidOperationException("No System Administrator left");

        return sysAdmins.Count == 1;
    }
}
