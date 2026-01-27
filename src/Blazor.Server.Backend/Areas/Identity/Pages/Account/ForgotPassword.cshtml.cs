using System.ComponentModel.DataAnnotations;
using Blazor.Server.Backend.Security;
using Core.Shared.Security;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sdk.Backend.Messaging;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Account;

public class ForgotPasswordModel(UserManager<SuiteUser> userManager, ISuiteMediator suiteMediator)
    : PageModel
{
    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [BindProperty]
    public InputModel Input { get; set; } = default!;

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class InputModel
    {
        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid)
        {
            var user = await userManager.FindByEmailAsync(Input.Email);
            if (user == null || !await userManager.IsEmailConfirmedAsync(user))
            {
                // Don't reveal that the user does not exist or is not confirmed
                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            var code = await userManager.GeneratePasswordResetTokenAsync(user);
            var callbackLink = this.CreateCallbackLink("/Account/ResetPassword", user.Id, code);

            await suiteMediator.Send(new SendResetPasswordLink(user.Id, callbackLink));

            return RedirectToPage("./ForgotPasswordConfirmation");
        }

        return Page();
    }
}
