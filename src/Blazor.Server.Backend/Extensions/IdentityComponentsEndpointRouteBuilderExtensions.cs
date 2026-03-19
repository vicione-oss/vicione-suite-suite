using Core.Shared.Extensions;
using Core.Shared.Security;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace Blazor.Server.Backend.Extensions;

internal static class IdentityComponentsEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapAdditionalIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var accountGroup = endpoints.MapGroup("/account");

        accountGroup.MapPost("/logout", async (
                [FromServices] SignInManager<SuiteUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.LocalRedirect(IdentityConstants.LoginRoute);
        });

        return accountGroup;
    }


    public static IEndpointRouteBuilder MapExternalIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var config = endpoints.ServiceProvider
            .GetRequiredService<IConfiguration>()
            .GetExternalIdProviderOptions();
        if (config == null)
            return endpoints;

        var accountGroup = endpoints.MapGroup("/account");

        MapLoginWithExternalProviderEndpoints(accountGroup);

        var manageGroup = accountGroup.MapGroup("/manage");

        MapAddingOfExternalLoginsEndpoints(manageGroup);

        return accountGroup;
    }

    private static void MapAddingOfExternalLoginsEndpoints(RouteGroupBuilder manageGroup)
    {
        manageGroup.MapPost("/linkExternalLogin",
            [Authorize] async (
                HttpContext context,
                [FromServices] SignInManager<SuiteUser> signInManager,
                [FromForm] string provider) =>
            {
                // Clear the existing external cookie to ensure a clean login process
                await context.SignOutAsync(Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme);

                var redirectUrl = UriHelper.BuildRelative(
                    context.Request.PathBase,
                    "/account/manage/externalLogins");

                var userId = signInManager.UserManager.GetUserId(context.User);
                var properties = signInManager.ConfigureExternalAuthenticationProperties(provider,
                    redirectUrl,
                    userId);
                return TypedResults.Challenge(properties, [provider]);
            });

        manageGroup.MapGet("/ExternalLogins",
            async (HttpContext context,
                ITempDataDictionaryFactory tempDataFactory,
                [FromServices] UserManager<SuiteUser> userManager,
                [FromServices] SignInManager<SuiteUser> signInManager) =>
            {
                var info = await signInManager.GetExternalLoginInfoAsync();
                var tempData = tempDataFactory.GetTempData(context);

                if (info == null)
                {
                    tempData["ExternalError"] = ExternalLoginError.LoginFailed;
                    tempData.Save();
                    return Results.LocalRedirect(IdentityConstants.LoginRoute);
                }

                var user = await userManager.GetUserAsync(context.User);
                if (user is null)
                {
                    tempData["ExternalError"] = ExternalLoginError.LoginFailed;
                    tempData.Save();
                    return Results.LocalRedirect("/");
                }

                var result = await userManager.AddLoginAsync(user, info);
                if (!result.Succeeded)
                    return Results.LocalRedirect(IdentityConstants.LoginRoute);

                // Clear the existing external cookie to ensure a clean login process
                await context.SignOutAsync(Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme);

                return Results.LocalRedirect("/");
            });
    }

    private static void MapLoginWithExternalProviderEndpoints(RouteGroupBuilder accountGroup)
    {
        accountGroup.MapPost("/performExternalLogin",
            (
                HttpContext context,
                [FromServices] SignInManager<SuiteUser> signInManager,
                [FromForm] string provider,
                [FromForm] string returnUrl = "/") =>
            {
                IEnumerable<KeyValuePair<string, StringValues>> query =
                [
                    new("ReturnUrl", returnUrl)
                ];

                var redirectUrl = UriHelper.BuildRelative(
                    context.Request.PathBase,
                    "/account/externalLogin",
                    QueryString.Create(query));

                var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
                return TypedResults.Challenge(properties, [provider]);
            });

        accountGroup.MapGet("/externalLogin",
            async (HttpContext context,
                ITempDataDictionaryFactory tempDataFactory,
                [FromServices] UserManager<SuiteUser> userManager,
                [FromServices] SignInManager<SuiteUser> signInManager,
                [FromQuery] string returnUrl) =>
            {
                var tempData = tempDataFactory.GetTempData(context);
                var info = await signInManager.GetExternalLoginInfoAsync();
                if (info == null)
                {
                    return Results.LocalRedirect(IdentityConstants.LoginRoute);
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
                    return Results.LocalRedirect(returnUrl);
                }

                if (signInResult.IsLockedOut)
                {
                    return Results.LocalRedirect(IdentityConstants.LoginRoute);
                }

                tempData["ExternalError"] = ExternalLoginError.UnknownExternalUser;
                tempData.Save();
                return Results.LocalRedirect(IdentityConstants.LoginRoute);
            });
    }
}
