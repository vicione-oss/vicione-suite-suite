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
        var userRoles = (await userManager.GetRolesAsync(user)).Select(r => new SuiteRole(r));

        // is it an sys admin at all?
        if (userRoles.All(r => r.Name != SeedingExtensions.AdminRoleName))
            return false;

        var sysAdmins = await userManager.GetUsersInRoleAsync(SeedingExtensions.AdminRoleName);
        if (sysAdmins.Count == 0)
            throw new InvalidOperationException("No System Administrator left");

        return sysAdmins.Count == 1;
    }

    public static async Task<SuiteUser> GetUserById(this UserManager<SuiteUser> userManager, string userId)
        => (await userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException("User not found"));
}
