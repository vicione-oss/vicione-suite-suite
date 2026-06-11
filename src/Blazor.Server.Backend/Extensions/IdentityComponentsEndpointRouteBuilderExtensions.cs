using Blazor.Server.Backend.Endpoints;
using Blazor.Shared;
using Blazor.Shared.Profile.Models;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement.AspNetCore;

namespace Blazor.Server.Backend.Extensions;

internal static class IdentityComponentsEndpointRouteBuilderExtensions
{
    extension(IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapPasskeyEndpoints()
        {
            var accountGroup = endpoints.MapGroup("/account");

            accountGroup.MapPost("/passkey-creation-options",
                    async (
                        HttpContext context,
                        [FromServices] UserManager<SuiteUser> userManager,
                        [FromServices] SignInManager<SuiteUser> signInManager,
                        [FromServices] IAntiforgery antiforgery) =>
                    {
                        await antiforgery.ValidateRequestAsync(context);

                        var user = await userManager.GetUserAsync(context.User);
                        if (user is null)
                        {
                            return Results.NotFound("Unable to load user");
                        }

                        var userId = await userManager.GetUserIdAsync(user);
                        var userName = await userManager.GetUserNameAsync(user) ?? "User";
                        var optionsJson = await signInManager.MakePasskeyCreationOptionsAsync(new()
                        {
                            Id = userId,
                            Name = userName,
                            DisplayName = userName
                        });
                        return TypedResults.Content(optionsJson, contentType: "application/json");
                    })
                .RequireAuthorization()
                .WithFeatureGate(Core.Shared.Features.Constants.PasskeyFeatureName);

            accountGroup.MapPost("/passkey-request-options",
                    async (
                        HttpContext context,
                        [FromServices] UserManager<SuiteUser> userManager,
                        [FromServices] SignInManager<SuiteUser> signInManager,
                        [FromServices] IAntiforgery antiforgery,
                        [FromQuery] string? username) =>
                    {
                        await antiforgery.ValidateRequestAsync(context);

                        var user = string.IsNullOrEmpty(username) ? null : await userManager.FindByNameAsync(username);
                        var optionsJson = await signInManager.MakePasskeyRequestOptionsAsync(user);
                        return TypedResults.Content(optionsJson, contentType: "application/json");
                    })
                .WithFeatureGate(Core.Shared.Features.Constants.PasskeyFeatureName);

            accountGroup.MapPost("/add-passkey",
                    async (HttpContext context,
                        [FromServices] UserManager<SuiteUser> userManager,
                        [FromServices] SignInManager<SuiteUser> signInManager,
                        [FromServices] IAntiforgery antiforgery,
                        [FromServices] ILogger<BlazorServerBackendModule> logger,
                        [FromForm] PasskeyInputModel Input) =>
                    {
                        await antiforgery.ValidateRequestAsync(context);

                        var user = await userManager.GetUserAsync(context.User);
                        if (user is null)
                        {
                            return Results.NotFound();
                        }

                        if (string.IsNullOrEmpty(Input.CredentialJson))
                        {
                            return Results.BadRequest();
                        }

                        var passkeys = await userManager.GetPasskeysAsync(user);
                        if (passkeys.Count >= Core.Shared.Passkeys.Constants.MaxPasskeyCount)
                        {
                            return Results.BadRequest();
                        }

                        var attestationResult
                            = await signInManager.PerformPasskeyAttestationAsync(Input.CredentialJson);
                        if (!attestationResult.Succeeded)
                        {
                            logger.LogError("Passkey attestation failed: {Error}", attestationResult.Failure.Message);
                            return Results.BadRequest(attestationResult.Failure.Message);
                        }

                        attestationResult.Passkey.Name = Input.Name;
                        var addPasskeyResult
                            = await userManager.AddOrUpdatePasskeyAsync(user, attestationResult.Passkey);

                        if (addPasskeyResult.Succeeded)
                            return Results.NoContent();

                        foreach (var identityError in addPasskeyResult.Errors)
                        {
                            logger.LogError("Passkey addition failed: {Code} {Description}",
                                identityError.Code,
                                identityError.Description);
                        }

                        return Results.BadRequest();
                    })
                .RequireAuthorization()
                .WithFeatureGate(Core.Shared.Features.Constants.PasskeyFeatureName);

            return endpoints;
        }


    public IEndpointRouteBuilder MapExternalIdentityEndpoints()
    {
        var accountGroup = endpoints.MapGroup(ExternalLogin.BaseRoute);

        MapLoginWithExternalProviderEndpoints(accountGroup);

        var manageGroup = accountGroup.MapGroup("/manage");

        MapAddingOfExternalLoginsEndpoints(manageGroup);

        return accountGroup;
    }

    public IEndpointConventionBuilder MapAdditionalIdentityEndpoints()
    {
            var accountGroup = endpoints.MapGroup("/account");

            accountGroup.MapPost("/logout",
                async (
                    [FromServices] SignInManager<SuiteUser> signInManager) =>
                {
                    await signInManager.SignOutAsync();
                    return Results.LocalRedirect(IdentityRoutes.LoginRoute);
                });

            return accountGroup;
        }
    }

    private static void MapAddingOfExternalLoginsEndpoints(RouteGroupBuilder manageGroup)
    {
        manageGroup.MapPost(ExternalLogin.LinkingFlow.InitiateLinkRoute,
            ManageExternalLogins.LinkExternalLoginHandler).RequireAuthorization();

        manageGroup.MapGet(ExternalLogin.LinkingFlow.LinkCallbackRoute,
            ManageExternalLogins.HandleLinkCallback).RequireAuthorization();
    }

    private static void MapLoginWithExternalProviderEndpoints(RouteGroupBuilder accountGroup)
    {
        accountGroup.MapPost(ExternalLogin.LoginFlow.InitiateLoginRoute,
            ExternalLogin.InitiateExternalLogin);

        accountGroup.MapGet(ExternalLogin.LoginFlow.LoginCallbackRoute,
            ExternalLogin.HandleLoginCallback);
    }
}
