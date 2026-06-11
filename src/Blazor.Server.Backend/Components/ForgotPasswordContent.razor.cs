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

public sealed partial class ForgotPasswordContent
{
    [Parameter]
    public bool BuildingLayout { get; set; }

    [SupplyParameterFromForm]
    private ForgotPasswordFormModel Input { get; set; } = default!;

    [Inject]
    private UserManager<SuiteUser> UserManager { get; set; } = default!;

    [Inject]
    private INavigationService NavigationService { get; set; } = default!;

    [Inject]
    private ISuiteMediator Mediator { get; set; } = default!;

    public async Task ResetPassword()
    {
        var user = await UserManager.FindByEmailAsync(Input.Email);
        if (user == null || !await UserManager.IsEmailConfirmedAsync(user))
        {
            // Don't reveal that the user does not exist or is not confirmed
            NavigationService.RedirectTo(IdentityRoutes.ForgotPasswordRoute);
            return;
        }

        var code = await UserManager.GeneratePasswordResetTokenAsync(user);
        var callbackLink = NavigationService.CreateCallbackLink(IdentityRoutes.ResetPasswordRoute, user.Id, code);

        await Mediator.Send(new SendResetPasswordLink(user.Id, callbackLink));

        NavigationService.RedirectTo(IdentityRoutes.ForgotPasswordRoute);
    }

    protected override void OnInitialized() => Input ??= new();
}
