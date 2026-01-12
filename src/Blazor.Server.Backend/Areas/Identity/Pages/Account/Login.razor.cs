using Blazor.Server.Backend.Areas.Identity.Pages.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

#pragma warning disable 8618 //required properties are not null!

namespace Blazor.Server.Backend.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed partial class Login
{
    //private string? errorMessage = string.Empty;

    //[Inject]
    //private SignInManager<SuiteUser> SignInManager { get; set; }

    //[Inject]
    //private IdentityRedirectManager RedirectManager { get; set; }

    //[Inject]
    //private ILogger<Login> Logger { get; set; }

    [SupplyParameterFromForm]
    private LoginFormModel Input { get; set; } = new();

    //[SupplyParameterFromQuery]
    //private string? ReturnUrl { get; set; }

    //[CascadingParameter]
    //private HttpContext HttpContext { get; set; } = default!;

    //public string? ReturnRoute { get; set; }

    protected override void OnInitialized()
    {
        //if (HttpMethods.IsGet(HttpContext.Request.Method))
        //{
        //    // Clear the existing external cookie to ensure a clean login process
        //    await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        //}
    }

    private void OnRememberMeKeyDown(KeyboardEventArgs args)
    {
        if (args.Code is "Enter" or "NumpadEnter")
            Input.RememberMe = !Input.RememberMe;
    }

    private void OnRememberMeClick()
        => Input.RememberMe = !Input.RememberMe;

    public void LoginUser()
    {
        //ReturnRoute ??= "/";

        //// This doesn't count login failures towards account lockout
        //// To enable password failures to trigger account lockout, set lockoutOnFailure: true
        //var result = await SignInManager.PasswordSignInAsync(Input.Username, Input.Password, Input.RememberMe, lockoutOnFailure: false);
        //if (result.Succeeded)
        //{
        //    Logger.LogInformation("User logged in");
        //    RedirectManager.RedirectTo(ReturnUrl);
        //}
        //if (result.RequiresTwoFactor)
        //{
        //    RedirectManager.RedirectTo(
        //        "Account/LoginWith2fa",
        //        new() { ["returnUrl"] = ReturnUrl, ["rememberMe"] = Input.RememberMe });
        //}
        //if (result.IsLockedOut)
        //{
        //    Logger.LogWarning("User account locked out.");
        //    RedirectManager.RedirectTo("Account/Lockout");
        //}
        //else
        //{
        //    errorMessage = "Error: Invalid login attempt.";
        //}
    }
}
