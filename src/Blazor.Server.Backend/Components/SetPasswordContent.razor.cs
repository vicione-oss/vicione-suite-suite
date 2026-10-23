using System.Globalization;
using Blazor.Server.Backend.Areas.Identity.Pages.Models;
using Blazor.Server.Backend.Extensions;
using Blazor.Server.Backend.Localization;
using Blazor.Shared;
using Blazor.Shared.Services;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;

namespace Blazor.Server.Backend.Components;

public sealed partial class SetPasswordContent
{
    private const string CancelFormId = "cancel-set-password";

    private EditContext _editContext = default!;
    private ValidationMessageStore? _messageStore;
    private SuiteUser? _user;

    [Inject]
    private SignInManager<SuiteUser> SignInManager { get; set; } = default!;

    [Inject]
    private UserManager<SuiteUser> UserManager { get; set; } = default!;

    [Inject]
    private INavigationService NavigationService { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public SetPasswordFormModel Input { get; set; } = default!;

    [Parameter]
    public bool BuildingLayout { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Input ??= new();

        _editContext = new EditContext(Input);
        _editContext.OnValidationRequested += HandleValidationRequested;
        _messageStore = new(_editContext);

        _user = await UserManager.GetUserAsync(SignInManager.Context.User);
        if (_user is null)
            NavigationService.RedirectTo(IdentityRoutes.LoginRoute);
        else if (await UserManager.HasPasswordAsync(_user))
            NavigationService.RedirectTo(IdentityRoutes.ChangePasswordRoute);
    }

    public async Task OnPostSubmit()
    {
        if (_user is null || !_editContext.Validate())
            return;

        if (Input.NewPassword != Input.ConfirmPassword)
        {
            _messageStore?.Add(() => Input.NewPassword, string.Format(CultureInfo.CurrentCulture, Common.ErrorInputsDoNotMatch, Common.NewPassword, Common.ConfirmPassword));
            _messageStore?.Add(() => Input.ConfirmPassword, string.Format(CultureInfo.CurrentCulture, Common.ErrorInputsDoNotMatch, Common.NewPassword, Common.ConfirmPassword));

            return;
        }

        ClearExpiredPasswordExpirationDate(_user);

        var result = await UserManager.AddPasswordAsync(_user, Input.NewPassword);
        if (!result.Succeeded)
        {
            ShowErrors(result.Errors);

            return;
        }

        // AddPasswordAsync changes the security stamp; without a refresh the stamp validation signs the user out.
        await SignInManager.RefreshSignInAsync(_user);

        NavigationService.RedirectTo("/");
    }

    private static void ClearExpiredPasswordExpirationDate(SuiteUser user)
    {
        if (user.PasswordExpirationDate <= DateTimeOffset.UtcNow)
            user.PasswordExpirationDate = null;
    }

    private void ShowErrors(IEnumerable<IdentityError> errors)
    {
        foreach (var error in errors)
        {
            _messageStore?.Add(() => Input.NewPassword, error.Description);
            if (error.IsPasswordPolicyError())
                _messageStore?.Add(() => Input.ConfirmPassword, error.Description);
        }
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e)
        => _messageStore?.Clear();
}
