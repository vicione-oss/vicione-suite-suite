using System.Globalization;
using Blazor.Server.Backend.Areas.Identity.Pages.Models;
using Blazor.Shared;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sdk.Client.Components.Wallpaper.Extensions;
using ViciOne.Ui.Localization.Resources;

#pragma warning disable 8618 //required properties are not null!
namespace Blazor.Server.Backend.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed class ChangePasswordModel(
    SignInManager<SuiteUser> signInManager,
    UserManager<SuiteUser> userManager) : PageModel
{
    private const string _currentUser = "current";
    private bool _isPersistent;

    [BindProperty]
    public ChangePasswordFormModel Input { get; set; }

    public string? ReturnRoute { get; set; }

    public string? PasswordExpiredMessage { get; set; }

    [TempData]
    private string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string username = _currentUser, bool isPersistent = false, string? returnRoute = null)
    {
        if (username == _currentUser)
            username = signInManager.Context.User.Identity?.Name ?? string.Empty;

        Input = new()
        {
            Username = username,
        };
        _isPersistent = isPersistent;

        SetLocalizations();
        SetBackgroundImagePath();

        if (!string.IsNullOrEmpty(ErrorMessage))
            ModelState.AddModelError(string.Empty, ErrorMessage);

        var user = await userManager.FindByNameAsync(Input.Username);
        if (user is not null)
        {
            if (user.PasswordExpirationDate <= DateTimeOffset.UtcNow)
                PasswordExpiredMessage = Localization.ChangePassword.PasswordExpiredMessage;
        }
        else
        {
            ModelState.AddModelError(string.Empty, Localization.ChangePassword.UserNotFound);
        }

        // This section is for the case, when the password of the user expires in a running session.
        // If this happens and the user clicks on 'Change password' they will be logged out to force them to change it and
        // ensure that the user can't access the Suite with an expired password.
        // After that the page gets reloaded with the appropriate parameters to ensure the user search without an authorized user.
        if (signInManager.IsSignedIn(signInManager.Context.User) && !string.IsNullOrEmpty(PasswordExpiredMessage))
        {
            await signInManager.SignOutAsync();

            return RedirectToPage("/Account/ChangePassword", new { Username = username, IsPersistent = _isPersistent });
        }

        returnRoute ??= Url.Content("~/");

        ReturnRoute = returnRoute;

        return Page();
    }

    public async Task<IActionResult> OnPostSubmitAsync(string? returnRoute = null)
    {
        SetLocalizations();
        SetBackgroundImagePath();

        returnRoute ??= Url.Content("~/");

        var user = await userManager.FindByNameAsync(Input.Username);

        if (user is not null && user.PasswordExpirationDate <= DateTimeOffset.UtcNow)
            PasswordExpiredMessage = Localization.ChangePassword.PasswordExpiredMessage;

        if (ModelState.IsValid)
        {
            if (Input.Password == Input.NewPassword)
            {
                ModelState.AddModelError(string.Empty, string.Format(CultureInfo.CurrentCulture, Localization.ChangePassword.ErrorInputsMustNotBeTheSame, CommonVocabulary.Password, Localization.ChangePassword.NewPassword));

                return Page();
            }
            else if (Input.NewPassword != Input.ConfirmNewPassword)
            {
                ModelState.AddModelError(string.Empty, string.Format(CultureInfo.CurrentCulture, Localization.ChangePassword.ErrorInputsDoNotMatch, Localization.ChangePassword.NewPassword, Localization.ChangePassword.ConfirmNewPassword));

                return Page();
            }

            if (user is null)
            {
                ModelState.AddModelError(string.Empty, Localization.ChangePassword.UserNotFound);

                return Page();
            }

            // This will only be updated in the database, when the password is verified within the ChangePasswordAsync method below.
            if (user.PasswordExpirationDate <= DateTimeOffset.UtcNow)
                user.PasswordExpirationDate = null;

            var result = await userManager.ChangePasswordAsync(user, Input.Password, Input.NewPassword);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                return Page();
            }

            // When a user without an expired password changes it, the user was never signed out, so we only refresh the current sign in.
            // When a user with an expired password changes it, the user will be logged in with Username and Password.
            if (string.IsNullOrEmpty(PasswordExpiredMessage))
                await signInManager.RefreshSignInAsync(user);
            else
                await signInManager.PasswordSignInAsync(Input.Username, Input.NewPassword, _isPersistent, false);

            return LocalRedirect(returnRoute);
        }

        return Page();
    }

    public IActionResult OnPostCancel(string? returnRoute = null)
        => LocalRedirect(returnRoute ?? Url.Content("~/"));

    private void SetLocalizations()
    {
        ViewData["Title"] = Localization.ChangePassword.Title;
        ViewData["Password"] = CommonVocabulary.Password;
        ViewData["NewPassword"] = Localization.ChangePassword.NewPassword;
        ViewData["ConfirmNewPassword"] = Localization.ChangePassword.ConfirmNewPassword;
        ViewData["Cancel"] = CommonVocabulary.Cancel;
    }

    private void SetBackgroundImagePath()
        => ViewData["BackgroundImagePath"] = Constants.WallpaperImage.GetPath(Constants.WallpaperBaseUri);
}
