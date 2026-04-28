using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Core.OS.UserManagement.Extensions;

internal static class UserManagerExtensions
{
    extension(UserManager<SuiteUser> userManager)
    {
        public async Task InvalidateLogins()
        {
            var users = userManager.Users.ToList();
            foreach (var user in users)
            {
                await userManager.UpdateSecurityStampAsync(user);
            }
        }

        public async Task<bool> IsLastSystemAdministrator(SuiteUser user)
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

        public async Task<SuiteUser> GetUserById(string userId)
            => (await userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException("User not found"));

        public async Task<SuiteUser?> FindByNormalizedUser(string? normalizedUserName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(normalizedUserName))
                return null;

            return await userManager.Users.AsNoTracking()
                .SingleOrDefaultAsync(u => u.NormalizedUserName == normalizedUserName, cancellationToken: cancellationToken);
        }
    }
}
