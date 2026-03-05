using Blazor.Server.Backend.Areas.Identity.Pages.Models;
using Blazor.Server.Backend.Security;
using Blazor.Shared.Services;
using Core.Shared.Security;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Sdk.Backend.Messaging;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed partial class ResendEmailConfirmation
{

    [Inject]
    private UserManager<SuiteUser> UserManager { get; set; } = default!;
    [Inject]
    private INavigationService NavigationService { get; set; } = default!;
    [Inject]
    private ISuiteMediator Mediator { get; set; } = default!;

    [SupplyParameterFromForm]
    private ResendEmailConfirmationFormModel Input { get; set; } = default!;

    public async Task ResendEmail()
    {
        var user = await UserManager.FindByEmailAsync(Input.Email);
        if (user == null || !await UserManager.IsEmailConfirmedAsync(user))
        {
            // Don't reveal that the user does not exist or is not confirmed
            NavigationService.RedirectTo(IdentityConstants.ConfirmMailRoute);
            return;
        }

        var code = await UserManager.GenerateEmailConfirmationTokenAsync(user);
        var callbackLink = NavigationService.CreateCallbackLink(IdentityConstants.ConfirmMailRoute, user.Id, code);

        await Mediator.Send(new SendResetPasswordLink(user.Id, callbackLink));

        NavigationService.RedirectTo("/account/forgot-password-confirmation");
    }

    protected override void OnInitialized() => Input ??= new();
}
