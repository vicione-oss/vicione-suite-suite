using System.Security.Claims;
using Blazor.Shared.UserManagement.Services.Validators;
using Core.Shared.UserManagement;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Blazor.Server.Backend.Services;

public sealed partial class ExternalLoginService(
    UserManager<SuiteUser> userManager,
    IUsernameValidator usernameValidator,
    ILogger<ExternalLoginService> logger)
{
    private const string ExternalAdminsGroupName = "vicione-oss";
    private const string EmailVerifiedClaim = "email_verified";
    private const string PreferredUsernameClaim = "preferred_username";
    private const string EmailClaim = "email";

    public async Task<SuiteUser?> CreateNewUserFromExternalLogin(ExternalLoginInfo info)
    {
        try
        {
            var email = ReadEmail(info);
            var userName = ReadUserName(info);

            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(email))
            {
                LogUsernameOrEmailNullOrEmpty();
                return null;
            }

            if (!usernameValidator.Validate(userName, out var errorMessage))
            {
                LogUsernameDoesNotMeetRequirementsError(userName, errorMessage);
                return null;
            }

            return await CreateNewUser(info, userName, email);
        }
        catch (Exception e)
        {
            LogFailedToCreateNewUserFromExternalLogin(e);
            return null;
        }
    }

    private static string? ReadEmail(ExternalLoginInfo info)
        => info.Principal.FindFirstValue(EmailClaim)
           ?? info.Principal.FindFirstValue(ClaimTypes.Email);

    /*
     * As we use the SuiteUser.UserName for login, the username should be unique and constant.
     * preferred_username is _not_ constant and discouraged by Microsoft for login:
     * https://learn.microsoft.com/en-us/entra/identity-platform/id-token-claims-reference#use-claims-to-reliably-identify-a-user
     *
     * Yet it is the best information we have for registering a new user currently.
     */
    private static string? ReadUserName(ExternalLoginInfo info)
        => info.Principal.FindFirstValue(PreferredUsernameClaim);

    private async Task<SuiteUser?> CreateNewUser(ExternalLoginInfo info, string userName, string email)
    {
        LogCreatingNewUserFromExternalLoginForUsernameAndEmail(logger, userName, email);
        _ = bool.TryParse(info.Principal.FindFirstValue(EmailVerifiedClaim), out bool emailConfirmed);
        var suiteUser = new SuiteUser
        {
            UserName = userName,
            Email = email,
            PasswordExpirationDate = DateTimeOffset.MinValue,
            EmailConfirmed = emailConfirmed
        };

        var result = await userManager.CreateAsync(suiteUser);
        if (!result.Succeeded)
        {
            logger.LogWarning("Failed to create new user from external login: {@Errors}", result.Errors);
            return null;
        }

        var addLoginResult = await userManager.AddLoginAsync(suiteUser, info);
        if (!addLoginResult.Succeeded)
        {
            logger.LogWarning("Failed to add external login to new user: {@Errors}", addLoginResult.Errors);
            await userManager.DeleteAsync(suiteUser);
            return null;
        }

        await AssignAdminRoleIfApplicable(info, suiteUser);
        return suiteUser;
    }

    private async Task AssignAdminRoleIfApplicable(ExternalLoginInfo info, SuiteUser suiteUser)
    {
        if (IsMemberOfViciOneDeveloperGroup(info) || IsTheVeryFirstUser(suiteUser))
        {
            LogUserWillBePromotedToAdminRole(logger, suiteUser.UserName!);
            await userManager.AddToRoleAsync(suiteUser, AuthorizationConstants.AdminRoleName);
        }
    }

    private static bool IsMemberOfViciOneDeveloperGroup(ExternalLoginInfo info)
    {
        var allGroups = info.Principal.FindAll("groups_direct");
        return allGroups.Select(c => c.Value).Contains(ExternalAdminsGroupName);
    }

    private bool IsTheVeryFirstUser(SuiteUser suiteUser) => userManager.Users.All(user => user.Id == suiteUser.Id);

    [LoggerMessage(LogLevel.Information,
        "Creating new user from external login for username {Username} and email {Email}")]
    static partial void LogCreatingNewUserFromExternalLoginForUsernameAndEmail(
        ILogger<ExternalLoginService> logger,
        string Username,
        string Email);

    [LoggerMessage(LogLevel.Information, "User {Username} will be promoted to admin role")]
    static partial void LogUserWillBePromotedToAdminRole(ILogger<ExternalLoginService> logger, string Username);

    [LoggerMessage(LogLevel.Warning, "Username or email null or empty")]
    partial void LogUsernameOrEmailNullOrEmpty();

    [LoggerMessage(LogLevel.Warning, "Username {Username} does not meet requirements: {Error}")]
    partial void LogUsernameDoesNotMeetRequirementsError(string username, string error);

    [LoggerMessage(LogLevel.Error, "Failed to create new user from external login")]
    partial void LogFailedToCreateNewUserFromExternalLogin(Exception exception);
}
