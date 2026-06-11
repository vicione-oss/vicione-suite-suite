using System.Globalization;
using System.Text;
using Blazor.Server.Backend.Areas.Identity.Pages.Models;
using Blazor.Server.Backend.Localization;
using Blazor.Shared;
using Blazor.Shared.Services;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace Blazor.Server.Backend.Components;

public sealed partial class ResetPasswordContent
{
    private EditContext editContext = default!;
    private ValidationMessageStore? messageStore;
    private string _code = string.Empty;

    [Inject]
    private SignInManager<SuiteUser> SignInManager { get; set; } = default!;

    [Inject]
    private UserManager<SuiteUser> UserManager { get; set; } = default!;

    [Inject]
    private INavigationService NavigationService { get; set; } = default!;

    [SupplyParameterFromForm]
    private ResetPasswordFormModel Input { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public string? Code { get; set; }

    [Parameter]
    public bool BuildingLayout { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (!string.IsNullOrEmpty(Code))
            _code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Code));

        Input ??= new();

        editContext = new EditContext(Input);
        editContext.OnValidationRequested += HandleValidationRequested;
        messageStore = new(editContext);
    }

    public async Task OnPostSubmit()
    {
        if (!editContext.Validate())
            return;

        var user = await UserManager.FindByEmailAsync(Input.Email);
        if (user == null || !await UserManager.IsEmailConfirmedAsync(user) || string.IsNullOrEmpty(_code))
        {
            // Don't reveal that the user does not exist
            NavigationService.RedirectTo(IdentityRoutes.ResetPasswordConfirmationRoute);
            return;
        }

        if (Input.NewPassword != Input.ConfirmNewPassword)
        {
            messageStore?.Add(() => Input.NewPassword, string.Format(CultureInfo.CurrentCulture, Common.ErrorInputsDoNotMatch, Common.NewPassword, Common.ConfirmNewPassword));
            messageStore?.Add(() => Input.ConfirmNewPassword, string.Format(CultureInfo.CurrentCulture, Common.ErrorInputsDoNotMatch, Common.NewPassword, Common.ConfirmNewPassword));

            return;
        }

        var result = await UserManager.ResetPasswordAsync(user, _code, Input.NewPassword);
        if (result.Succeeded)
        {
            NavigationService.RedirectTo(IdentityRoutes.ResetPasswordConfirmationRoute);
            return;
        }

        foreach (var error in result.Errors)
        {
            switch (error.Code)
            {
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
                    messageStore?.Add(() => Input.NewPassword, error.Description);
                    break;
            }
        }
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e)
        => messageStore?.Clear();
}
