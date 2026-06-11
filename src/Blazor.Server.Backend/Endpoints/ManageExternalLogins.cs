using Blazor.Server.Backend.Extensions;
using Blazor.Shared;
using Core.Shared.Security;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Blazor.Server.Backend.Endpoints;

public static class ManageExternalLogins
{
    private const string RootUrl = "/";

    public static async Task<IResult> LinkExternalLoginHandler(HttpContext context,
        [FromServices] SignInManager<SuiteUser> signInManager,
        [FromForm] string provider)
    {
        // Clear the existing external cookie to ensure a clean login process
        await context.SignOutAsync(Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme);

        var redirectUrl = UriHelper.BuildRelative(
            context.Request.PathBase,
            "/account/manage/external-logins");

        var userId = signInManager.UserManager.GetUserId(context.User);
        var properties = signInManager.ConfigureExternalAuthenticationProperties(provider,
            redirectUrl,
            userId);
        return TypedResults.Challenge(properties, [provider]);
    }

    public static async Task<IResult> HandleLinkCallback(HttpContext context,
        ITempDataDictionaryFactory tempDataFactory,
        [FromServices] UserManager<SuiteUser> userManager,
        [FromServices] SignInManager<SuiteUser> signInManager)
    {
        var info = await signInManager.GetExternalLoginInfoAsync();

        if (info == null)
        {
            tempDataFactory.AddExternalError(context, ExternalLoginError.LoginFailed);
            return IdentityRoutes.LoginRoute.ToLocalRedirect();
        }

        var user = await userManager.GetUserAsync(context.User);
        if (user is null)
        {
            tempDataFactory.AddExternalError(context, ExternalLoginError.NoLocalUser);
            return RootUrl.ToLocalRedirect();
        }

        var result = await userManager.AddLoginAsync(user, info);
        if (!result.Succeeded)
        {
            tempDataFactory.AddExternalError(context, ExternalLoginError.ExternalAccountAlreadyAssociated);
            return IdentityRoutes.LoginRoute.ToLocalRedirect();
        }

        await signInManager.RefreshSignInAsync(user);

        // Clear the existing external cookie to ensure a clean login process
        await context.SignOutAsync(Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme);

        // TODO https://gitlab.com/vicione-oss/vicione/suite/suite/-/work_items/2786:
        // Replace "/" with deep link to settings > profile > external-accounts
        return RootUrl.ToLocalRedirect();
    }
}
