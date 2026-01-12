using Blazor.Server.Backend.Areas.Identity.Pages.Models;
using Blazor.Server.Backend.Security;
using Blazor.Shared;
using Core.Shared.Mail;
using Core.Shared.Security;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;
using Sdk.Client.Components.Wallpaper.Extensions;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed class LoginModel(
    SignInManager<SuiteUser> signInManager,
    UserManager<SuiteUser> userManager,
    IAccountVerification accountVerification,
    ISuiteMediator suiteMediator,
    IMailSenderStatus senderStatus,
    ILogger<LoginModel> logger) : PageModel
{
    public IMailSenderStatus SenderStatus { get; } = senderStatus;

    [BindProperty]
    public LoginFormModel Input { get; set; } = default!;

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

        if (!ModelState.IsValid)
            return Page();

        var user = await userManager.FindByNameAsync(Input.Username);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "User not found");
        }

        if (user?.UserName is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return Page();
        }

        // This doesn't count login failures towards account lockout
        // To enable password failures to trigger account lockout, set lockoutOnFailure: true
        var result = await signInManager.PasswordSignInAsync(user.UserName,
            Input.Password,
            Input.RememberMe,
            lockoutOnFailure: false);

        if (result.Succeeded)
        {
            logger.LogInformation("User logged in");

            if (user.PasswordExpirationDate <= DateTimeOffset.UtcNow)
            {
                var username = user.UserName;
                await signInManager.SignOutAsync();

                return RedirectToPage("/Account/ChangePassword",
                    new
                    {
                        Username = username,
                        IsPersistent = Input.RememberMe
                    });
            }

            return LocalRedirect(returnRoute);
        }

        if (result.IsNotAllowed && await IsAccountVerificationNeeded(user.Id))
        {
            logger.LogInformation("User logged in but is not yet verified");
            var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
            await SendVerificationEmail(user, code);

            return RedirectToPage("/Account/RegisterConfirmation");
        }

        if (result.IsLockedOut)
        {
            logger.LogWarning("User account locked out");
            return RedirectToPage("./Lockout");
        }

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return Page();

        // If we got this far, something failed, redisplay form
    }

    private Task SendVerificationEmail(SuiteUser user, string code)
        => suiteMediator.Send(new SendVerifyEmailAddressLink(user.Id,
            this.CreateCallbackLink("/Account/ConfirmEmail", user.Id, code)));

    private Task<bool> IsAccountVerificationNeeded(string userId)
        => accountVerification
            .NeedsVerification(userId);

    private void SetLocalizations()
    {
        ViewData["Title"] = Localization.Login.Title;
        ViewData["User"] = CommonVocabulary.User;
        ViewData["Password"] = CommonVocabulary.Password;
        ViewData["Disconnected"] = Localization.Login.WaitingForConnection;
    }

    private void SetBackgroundImagePath()
        => ViewData["BackgroundImagePath"] = Constants.WallpaperImage.GetPath(Constants.WallpaperBaseUri);
}
