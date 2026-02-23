using Core.Shared.Mail;
using Core.Shared.Security;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;

namespace Core.OS.UserManagement.Security;

public class AccountVerification(
    IMailSenderStatus mailSenderStatus,
    ISecuritySettings securitySettings,
    UserManager<SuiteUser> userManager)
    : IAccountVerification
{
    public async Task<bool> NeedsVerification(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await GetUserById(userId);

        var usersNeedToBeVerified =
            mailSenderStatus.IsConfigured() &&
            securitySettings.RequireAccountVerification;

        if (usersNeedToBeVerified)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return !await userManager.IsEmailConfirmedAsync(user);
        }

        return false;
    }

    private async Task<SuiteUser> GetUserById(string userId)
        => (await userManager.FindByIdAsync(userId) ?? throw new ArgumentException("User not found"));
}
