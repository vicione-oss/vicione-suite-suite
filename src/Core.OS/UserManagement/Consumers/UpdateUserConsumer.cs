using System.Security.Claims;
using Core.OS.UserManagement.Extensions;
using Core.Shared;
using Core.Shared.UserManagement.Commands;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using Core.Shared.UserManagement.Extensions;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Sdk.Authorization;
using Sdk.Messaging;
using Sdk.Modules;

namespace Core.OS.UserManagement.Consumers;

public sealed partial class UpdateUserConsumer(UserManager<SuiteUser> userManager, RoleManager<SuiteRole> roleManager,
    IEqualityComparer<Claim> claimEqualityComparer, ILogger<UpdateUserConsumer> logger)
        : IConsumer<UpdateUser>
{
    public async Task Consume(ConsumeContext<UpdateUser> context)
    {
        var correlationId = context.Message.CorrelationId;
        var desiredProfile = context.Message.UserProfile;

        try
        {
            var existingSuiteUser = await userManager.FindByNameAsync(desiredProfile.UserName.Value);
            if (existingSuiteUser is null)
            {
                var existsError = new ErrorInfo(UserErrorCodes.UpdateFailedNotFound,
                                 $"Could not update user '{desiredProfile.UserName}'. No user was found with that name");
                var errorResponse = new UserUpdatedEvent(desiredProfile, desiredProfile)
                {
                    CorrelationId = correlationId,
                    ErrorInfo = existsError
                };

                await context.Publish(errorResponse, context.CancellationToken);
                return;
            }

            var profileBefore = await existingSuiteUser.CreateUserProfile(userManager);

            existingSuiteUser.AssignOptionalData(desiredProfile);
            if (existingSuiteUser.Email != desiredProfile.Email)
                existingSuiteUser.Email = desiredProfile.Email;

            await userManager.UpdateAsync(existingSuiteUser);

            var errorInfo = await AssignNewPassword(desiredProfile, existingSuiteUser, context.Message.RequestingUserName);
            if (errorInfo is not null)
            {
                var errorResponse = new UserUpdatedEvent(desiredProfile, profileBefore)
                {
                    CorrelationId = correlationId,
                    ErrorInfo = errorInfo
                };

                await context.Publish(errorResponse, context.CancellationToken);
                return;
            }


            await AssignRoles(desiredProfile, existingSuiteUser);
            await AssignClaims(desiredProfile, existingSuiteUser);

            var response = new UserUpdatedEvent(desiredProfile, profileBefore)
            {
                CorrelationId = correlationId
            };

            await context.Publish(response, context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogError(logger, ex, context.Message.UserProfile.UserName.Value, correlationId);

            var errorInfo = new ErrorInfo(UserErrorCodes.UpdateFailed,
                         $"Could not update user '{context.Message.UserProfile.UserName}'. {ex.Message}.");
            var errorResponse = new UserUpdatedEvent(desiredProfile, desiredProfile)
            {
                CorrelationId = correlationId,
                ErrorInfo = errorInfo
            };

            await context.Publish(errorResponse, context.CancellationToken);
        }
    }

    private async Task<ErrorInfo?> AssignNewPassword(UserProfile desiredProfile, SuiteUser existingSuiteUser, string requestingUserName)
    {
        if (desiredProfile.NewPassword is null)
            return null;

        IdentityResult result;
        if (desiredProfile.CurrentPassword is not null)
        {
            if (string.Equals(desiredProfile.CurrentPassword, desiredProfile.NewPassword, StringComparison.Ordinal))
                return new ErrorInfo(UserErrorCodes.UpdateFailed, "Your new password cannot be the same as your current password.");

            result = await userManager.ChangePasswordAsync(existingSuiteUser,
                desiredProfile.CurrentPassword,
                desiredProfile.NewPassword);
        }
        else if (existingSuiteUser.UserName == requestingUserName)
        {
            return new ErrorInfo(UserErrorCodes.UpdateFailed, "Current password must be provided.");
        }
        else
        {
            var requestingUser = await userManager.FindByNameAsync(requestingUserName);
            if (requestingUser is null)
                return null;
            var sysadminClaim = ModuleAuthorizationClaimFactory.CreateClaim(Constants.SystemModuleId, AccessLevel.Full, ModuleIdResolver.GetModuleName(Constants.SystemModuleId));
            var hasClaim = await HasClaimIncludingRolesAsync(userManager,
                roleManager,
                requestingUser,
                sysadminClaim.Type,
                sysadminClaim.Value);
            if (!hasClaim)
                return new ErrorInfo(UserErrorCodes.UpdateFailed, "Only administrators may change another user's password.");

            result = await userManager.RemovePasswordAsync(existingSuiteUser);
            if (result.Succeeded)
                result = await userManager.AddPasswordAsync(existingSuiteUser, desiredProfile.NewPassword);
        }

        if (result.Succeeded)
            return null;
        return new ErrorInfo(UserErrorCodes.UpdateFailedPassword,
                    $"Could not update user '{desiredProfile.UserName}'. Error: {string.Join(Environment.NewLine, result.Errors.Select(e => e.Description))}");
    }

    private async Task AssignRoles(UserProfile desiredProfile, SuiteUser existingSuiteUser)
    {
        if (await userManager.IsLastSystemAdministrator(existingSuiteUser)
            && !desiredProfile.Roles.Contains(SeedingExtensions.AdminRoleName))
        {
            desiredProfile.Roles.Add(SeedingExtensions.AdminRoleName);
            logger.LogWarning("Skipping remove sys admin role from user {User}", existingSuiteUser.UserName);
        }

        foreach (var suiteRole in roleManager.Roles.Select(r => r.Name!))
        {
            if (desiredProfile.Roles.Contains(suiteRole))
            {
                await userManager.AddToRoleAsync(existingSuiteUser, suiteRole);
                continue;
            }

            if (!await userManager.IsInRoleAsync(existingSuiteUser, suiteRole))
                continue;

            await userManager.RemoveFromRoleAsync(existingSuiteUser, suiteRole);
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

    private static async Task<bool> HasClaimIncludingRolesAsync(
        UserManager<SuiteUser> userManager,
        RoleManager<SuiteRole> roleManager,
        SuiteUser user,
        string claimType,
        string claimValue)
    {
        var userClaims = await userManager.GetClaimsAsync(user);
        if (userClaims.Any(c => c.Type == claimType && c.Value == claimValue))
            return true;

        var roles = await userManager.GetRolesAsync(user);
        foreach (var roleName in roles)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role == null)
                continue;

            var roleClaims = await roleManager.GetClaimsAsync(role);
            if (roleClaims.Any(c => c.Type == claimType && c.Value == claimValue))
                return true;
        }

        return false;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to update user '{name}' correlated by '{correlationId}'.")]
    private static partial void LogError(ILogger<UpdateUserConsumer> logger, Exception ex, string name, Guid correlationId);
}
