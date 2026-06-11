using Blazor.Server.Backend.Areas.Identity.Pages.Models;
using Blazor.Server.Backend.Security;
using Blazor.Shared;
using Blazor.Shared.Services;
using Core.Shared.Security;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Sdk.Backend.Messaging;

namespace Blazor.Server.Backend.Components;

public sealed partial class ResendEmailConfirmationContent
{
    [Parameter]
    public bool BuildingLayout { get; set; }

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
        if (user == null || await UserManager.IsEmailConfirmedAsync(user))
        {
            // Don't reveal that the user does not exist or is already confirmed
            NavigationService.RedirectTo(IdentityRoutes.EmailConfirmationRoute);
            return;
        }

        var code = await UserManager.GenerateEmailConfirmationTokenAsync(user);
        var callbackLink = NavigationService.CreateCallbackLink(IdentityRoutes.ConfirmMailRoute, user.Id, code);

        await Mediator.Send(new SendVerifyEmailAddressLink(user.Id, callbackLink));

        NavigationService.RedirectTo(IdentityRoutes.EmailConfirmationRoute);
    }

    protected override void OnInitialized() => Input ??= new();
}

