using System.Globalization;
using Blazor.Server.Backend.Areas.Identity.Pages.Models;
using Blazor.Shared.Services;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed partial class ChangePassword
{
    private EditContext editContext = default!;
    private ValidationMessageStore? messageStore;
    private bool _passwordExpired;

    [Inject]
    private SignInManager<SuiteUser> SignInManager { get; set; } = default!;
    [Inject]
    private UserManager<SuiteUser> UserManager { get; set; } = default!;
    [Inject]
    private INavigationService NavigationService { get; set; } = default!;

    [SupplyParameterFromForm]
    private ChangePasswordFormModel Input { get; set; } = default!;

    [SupplyParameterFromQuery]
    private string? User { get; set; }
    [SupplyParameterFromQuery]
    private bool IsPersistent { get; set; }
    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (string.IsNullOrEmpty(User))
            User = SignInManager.Context.User.Identity?.Name ?? string.Empty;

        Input ??= new()
        {
            Username = User,
        };

        if (string.IsNullOrEmpty(Input.Username))
            Input.Username = User;

        editContext = new EditContext(Input);
        editContext.OnValidationRequested += HandleValidationRequested;
        messageStore = new(editContext);

        var user = await UserManager.FindByNameAsync(Input.Username);
        if (user is not null)
        {
            if (user.PasswordExpirationDate <= DateTimeOffset.UtcNow)
                _passwordExpired = true;
        }
        else
        {
            messageStore.Add(() => Input.Username, Localization.ChangePassword.UserNotFound);
        }

        // This section is for the case, when the password of the user expires in a running session.
        // If this happens and the user clicks on 'Change password' they will be logged out to force them to change it and
        // ensure that the user can't access the Suite with an expired password.
        // After that the page gets reloaded with the appropriate parameters to ensure the user search without an authorized user.
        if (SignInManager.IsSignedIn(SignInManager.Context.User) && _passwordExpired)
        {
            await SignInManager.SignOutAsync();

            NavigationService.RedirectTo(
                IdentityConstants.ChangePasswordRoute,
                new Dictionary<string, object?>()
                {
                    { nameof(User), Input.Username },
                    { nameof(IsPersistent), IsPersistent }
                });
        }
    }

    public async Task OnPostSubmit()
    {
        ReturnUrl ??= "/";

        var user = await UserManager.FindByNameAsync(Input.Username);

        if (user is not null && user.PasswordExpirationDate <= DateTimeOffset.UtcNow)
            _passwordExpired = true;

        if (!editContext.Validate())
            return;

        if (Input.Password == Input.NewPassword)
        {
            messageStore?.Add(() => Input.Password, string.Format(CultureInfo.CurrentCulture, Localization.ChangePassword.ErrorInputsMustNotBeTheSame, CommonVocabulary.Password, Localization.Common.NewPassword));
            messageStore?.Add(() => Input.NewPassword, string.Format(CultureInfo.CurrentCulture, Localization.ChangePassword.ErrorInputsMustNotBeTheSame, CommonVocabulary.Password, Localization.Common.NewPassword));

            return;
        }
        else if (Input.NewPassword != Input.ConfirmNewPassword)
        {
            messageStore?.Add(() => Input.NewPassword, string.Format(CultureInfo.CurrentCulture, Localization.Common.ErrorInputsDoNotMatch, Localization.Common.NewPassword, Localization.Common.ConfirmNewPassword));
            messageStore?.Add(() => Input.ConfirmNewPassword, string.Format(CultureInfo.CurrentCulture, Localization.Common.ErrorInputsDoNotMatch, Localization.Common.NewPassword, Localization.Common.ConfirmNewPassword));

            return;
        }

        if (user is null)
        {
            messageStore?.Add(() => Input.Username, Localization.ChangePassword.UserNotFound);
            return;
        }

        // This will only be updated in the database, when the password is verified within the ChangePasswordAsync method below.
        if (user.PasswordExpirationDate <= DateTimeOffset.UtcNow)
            user.PasswordExpirationDate = null;

        var result = await UserManager.ChangePasswordAsync(user, Input.Password, Input.NewPassword);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                switch (error.Code)
                {
                    case nameof(IdentityErrorDescriber.PasswordMismatch):
                        messageStore?.Add(() => Input.Password, error.Description);
                        break;
                    case nameof(IdentityErrorDescriber.PasswordRequiresDigit):
                    case nameof(IdentityErrorDescriber.PasswordRequiresLower):
                    case nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric):
                    case nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars):
                    case nameof(IdentityErrorDescriber.PasswordRequiresUpper):
                    case nameof(IdentityErrorDescriber.PasswordTooShort):
                        messageStore?.Add(() => Input.NewPassword, error.Description);
                        messageStore?.Add(() => Input.ConfirmNewPassword, error.Description);
                        break;
                    default:
                        messageStore?.Add(() => Input.Username, error.Description);
                        break;
                }
            }

            return;
        }

        // When a user without an expired password changes it, the user was never signed out, so we only refresh the current sign in.
        // When a user with an expired password changes it, the user will be logged in with Username and Password.
        if (!_passwordExpired)
            await SignInManager.RefreshSignInAsync(user);
        else
            await SignInManager.PasswordSignInAsync(Input.Username, Input.NewPassword, IsPersistent, false);

        NavigationService.RedirectTo(ReturnUrl);
    }

    public void OnCancel()
        => NavigationService.RedirectTo(ReturnUrl ?? "/");

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e)
        => messageStore?.Clear();
}
