using Blazor.Server.Backend.Areas.Identity.Pages.Models;
using Blazor.Shared;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Sdk.Client.Components.Wallpaper.Extensions;
using ViciOne.Ui.Localization.Resources;

#pragma warning disable 8618 //required properties are not null!

namespace Blazor.Server.Backend.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed class LoginModel(
    SignInManager<SuiteUser> signInManager,
    UserManager<SuiteUser> userManager,
    ILogger<LoginModel> logger) : PageModel
{
    [BindProperty]
    public LoginFormModel Input { get; set; }

    //public IList<AuthenticationScheme> ExternalLogins { get; set; }

    public string? ReturnRoute { get; set; }

    [TempData]
    private string? ErrorMessage { get; set; }

    public override PageResult Page()
    {
        SetLocalizations();
        SetBackgroundImagePath();

        return base.Page();
    }

    public async Task OnGetAsync(string? returnRoute = null)
    {
        SetLocalizations();
        SetBackgroundImagePath();

        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        returnRoute ??= Url.Content("~/");

        // Clear the existing external cookie to ensure a clean login process
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        //ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        ReturnRoute = returnRoute;
    }

    public async Task<IActionResult> OnPostAsync(string? returnRoute = null)
    {
        returnRoute ??= Url.Content("~/");

        //ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        if (ModelState.IsValid)
        {
            var user = await userManager.FindByNameAsync(Input.Username);
            if (user?.UserName is null)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }

            // This doesn't count login failures towards account lockout
            // To enable password failures to trigger account lockout, set lockoutOnFailure: true
            var result = await signInManager.PasswordSignInAsync(user.UserName, Input.Password, Input.RememberMe, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                logger.LogInformation("User logged in");

                if (user.PasswordExpirationDate <= DateTimeOffset.UtcNow)
                {
                    var username = user.UserName;
                    await signInManager.SignOutAsync();

                    return RedirectToPage("/Account/ChangePassword", new { Username = username, IsPersistent = Input.RememberMe });
                }

                return LocalRedirect(returnRoute);
            }
            if (result.RequiresTwoFactor)
            {
                return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnRoute, Input.RememberMe });
            }
            if (result.IsLockedOut)
            {
                logger.LogWarning("User account locked out");
                return RedirectToPage("./Lockout");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }
        }

        // If we got this far, something failed, redisplay form
        return Page();
    }

    private void SetLocalizations()
    {
        ViewData["Title"] = Localization.Login.Title;
        ViewData["User"] = CommonVocabulary.User;
        ViewData["Password"] = CommonVocabulary.Password;
    }

    private void SetBackgroundImagePath()
        => ViewData["BackgroundImagePath"] = Constants.WallpaperImage.GetPath(Constants.WallpaperBaseUri);
}
