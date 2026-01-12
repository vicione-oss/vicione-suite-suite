using System.Data;
using System.Security.Claims;
using Core.OS.UserManagement.Extensions;
using Core.Shared.UserManagement.Commands;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using Core.Shared.UserManagement.Extensions;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Sdk.Messaging;

namespace Core.OS.UserManagement.Consumers;

public sealed class UpdateUserConsumer(UserManager<SuiteUser> userManager, RoleManager<IdentityRole> roleManager,
    IEqualityComparer<Claim> claimEqualityComparer, ILogger<UpdateUserConsumer> logger)
        : IConsumer<UpdateUser>
{
    public async Task Consume(ConsumeContext<UpdateUser> context)
    {
        var correlationId = context.CorrelationId ?? Guid.Empty;

        logger.LogInformation("Consuming {Command} with CorrelationId '{Id}'", nameof(UpdateUser), correlationId);

        var profile = context.Message.UserProfile;

        var suiteUser = await userManager.FindByNameAsync(profile.UserName.Value);
        if (suiteUser is null)
        {
            await context.Publish(new UserErrorEvent(correlationId, new ErrorInfo(UserErrorEvent.UpdateFailedNotFound,
                    $"Could not update user '{profile.UserName}'. No user was found with that name"),
                profile.UserName));
            return;
        }

        var profileBefore = await suiteUser.CreateUserProfile(userManager);

        suiteUser.AssignOptionalData(profile);
        if (suiteUser.Email != profile.Email)
            suiteUser.Email = profile.Email;

        await userManager.UpdateAsync(suiteUser);

        var errorInfo = await AssignNewPassword(profile, suiteUser);
        if (errorInfo is not null)
        {
            await context.Publish(new UserErrorEvent(correlationId, errorInfo, profile.UserName));
            return;
        }

        await AssignRoles(profile, suiteUser);
        await AssignClaims(profile, suiteUser);

        await context.Publish(new UserUpdatedEvent(correlationId, profile, profileBefore));
    }

    private async Task<ErrorInfo?> AssignNewPassword(UserProfile profile, SuiteUser suiteUser)
    {
        if (profile.CurrentPassword is null || profile.NewPassword is null)
            return null;

        if (string.Equals(profile.CurrentPassword, profile.NewPassword, StringComparison.Ordinal))
        {
            return new ErrorInfo(UserErrorEvent.UpdateFailed,
                    "Your new password cannot be the same as your current password.");
        }

        var result = await userManager.ChangePasswordAsync(suiteUser,
            profile.CurrentPassword,
            profile.NewPassword);

        if (result.Succeeded)
            return null;

        return new ErrorInfo(UserErrorEvent.UpdateFailedPassword,
                    $"Could not update user '{profile.UserName}'. Error: {string.Join(Environment.NewLine, result.Errors.Select(e => e.Description))}");
    }

    private async Task AssignRoles(UserProfile profile, SuiteUser suiteUser)
    {
        var sysAdminRoleName = SeedingExtensions.GetSystemAdministratorRoleName();

        foreach (var role in roleManager.Roles.Select(r => r.Name!))
        {
            if (profile.Roles.Contains(new Role(role)))
            {
                await userManager.AddToRoleAsync(suiteUser, role);
                continue;
            }

            if (!await userManager.IsInRoleAsync(suiteUser, role))
                continue;

            // not sys admin role can be removed safely
            if (role != sysAdminRoleName)
            {
                await userManager.RemoveFromRoleAsync(suiteUser, role);
                continue;
            }

            if (!await userManager.IsLastSystemAdministrator(suiteUser))
            {
                await userManager.RemoveFromRoleAsync(suiteUser, role);
                continue;
            }

            logger.LogWarning("Skip remove sys admin role from user {User}", suiteUser.UserName);

            // ensure it's added again so UI state event is correct
            profile.Roles.Add(new Role(sysAdminRoleName));
        }
    }

    private async Task AssignClaims(UserProfile profile, SuiteUser suiteUser)
    {
        var existingClaims = await userManager.GetClaimsAsync(suiteUser);
        var intendedClaims = profile.Claims.Select(userProfileClaim => userProfileClaim.ToClaim()).ToArray();

        // remove obsolete claims
        {
            foreach (var existingClaim in existingClaims)
            {
                if (intendedClaims.Contains(existingClaim, claimEqualityComparer))
                    continue;

                await userManager.RemoveClaimAsync(suiteUser, existingClaim);
            }
        }

        // add new claims
        {
            foreach (var intendedClaim in intendedClaims)
            {
                if (existingClaims.Contains(intendedClaim, claimEqualityComparer))
                    continue;

                await userManager.AddClaimAsync(suiteUser, intendedClaim);
            }
        }
    }
}
