using Blazor.Server.Backend.Extensions;
using Blazor.Server.Backend.Services;
using Blazor.Shared;
using Core.Shared.Security;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Blazor.Server.Backend.Endpoints;

public static partial class ExternalLogin
{
    public const string BaseRoute = "/account";

    /// <summary>
    /// Contains routing constants for managing the process of linking external login services
    /// to an existing user account in the application.
    /// </summary>
    public static class LinkingFlow
    {
        public const string InitiateLinkRoute = "/link-external-login";
        public const string LinkCallbackRoute = "/external-logins";
    }

    /// <summary>
    /// Contains routing constants for managing the external login process,
    /// including initiating the login with an external provider and handling callbacks
    /// after authentication.
    /// </summary>
    public static class LoginFlow
    {
        public const string InitiateLoginRoute = "/perform-external-login";
        public const string LoginCallbackRoute = "/external-login";
    }

    public static async Task<IResult> HandleLoginCallback(HttpContext context,
        ITempDataDictionaryFactory tempDataFactory,
        [FromServices] SignInManager<SuiteUser> signInManager,
        [FromServices] ExternalLoginService externalLoginService,
        [FromServices] ILogger<ExternalLoginService> logger,
        [FromQuery] Uri returnUrl)
    {
        var info = await signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            logger.LogWarning("external login failed: no info. Returning to login.");
            return Results.LocalRedirect(
                IdentityRoutes.LoginRoute);
        }

        var signInResult = await signInManager.ExternalLoginSignInAsync(
            info.LoginProvider,
            info.ProviderKey,
            isPersistent: false,
            bypassTwoFactor: true
        );

        if (signInResult.Succeeded)
        {
            await context.SignOutAsync(Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme);
            return Results.LocalRedirect(returnUrl.ToString());
        }

        if (signInResult.IsLockedOut)
        {
            logger.LogWarning("external login failed: user is locked out. Returning to login.");

            return Results.LocalRedirect(
                IdentityRoutes.LoginRoute);
        }

        var existingUser = await signInManager.UserManager.FindByLoginAsync(
            info.LoginProvider,
            info.ProviderKey);
        if (existingUser is not null)
        {
            logger.LogExternalLoginFailedUserAlreadyExists(signInResult);
            return Results.LocalRedirect(
                IdentityRoutes.LoginRoute);
        }

        var newUser = await externalLoginService.CreateNewUserFromExternalLogin(info);
        if (newUser is null)
        {
            logger.LogError("external login failed: new user creation failed. Returning to login.");
            tempDataFactory.AddExternalError(context,
                ExternalLoginError.NewUserCreationFailed);

            return Results.LocalRedirect(
                IdentityRoutes.LoginRoute);
        }

        await context.SignOutAsync(Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme);
        await signInManager.SignInAsync(newUser, isPersistent: false);

        return Results.LocalRedirect(returnUrl.ToString());
    }

    public static IResult InitiateExternalLogin(
        HttpContext context,
        [FromServices] SignInManager<SuiteUser> signInManager,
        [FromForm] string provider,
        [FromForm] Uri returnUrl)
    {
        IEnumerable<KeyValuePair<string, StringValues>> query =
        [
            new("returnUrl", returnUrl.ToString())
        ];

        var redirectUrl = UriHelper.BuildRelative(
            context.Request.PathBase,
            $"{BaseRoute}{LoginFlow.LoginCallbackRoute}",
            QueryString.Create(query));

        var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return TypedResults.Challenge(properties, [provider]);
    }

    [LoggerMessage(LogLevel.Warning, "external login failed: user already exists. Returning to login. {Result}")]
    static partial void LogExternalLoginFailedUserAlreadyExists(this ILogger<ExternalLoginService> logger,
        SignInResult result);
}
