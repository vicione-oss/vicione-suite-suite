using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Core.Shared;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

#pragma warning disable 8618 //required properties are not null!

namespace Blazor.Server.Backend.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed class RegisterModel(
    UserManager<SuiteUser> userManager,
    SignInManager<SuiteUser> signInManager,
    ILogger<RegisterModel> logger,
    IEmailSender emailSender) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; }

    public string? ReturnRoute { get; set; }

    //public IList<AuthenticationScheme> ExternalLogins { get; set; }

    public Task OnGetAsync(string? returnPath = null)
    {
        ReturnRoute = returnPath;
        //ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        return Task.CompletedTask;
    }

    public async Task<IActionResult> OnPostAsync(string? returnPath = null)
    {
        returnPath ??= Url.Content("~/");

        if (!ModelState.IsValid)
            return Page();

        var user = new SuiteUser
        {
            UserName = Input.Username,
            Email = Input.Email
        };
        var result = await userManager.CreateAsync(user, Input.Password);
        if (result.Succeeded)
        {
            logger.LogInformation("User created a new account with password");

            var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new
                {
                    area = "Identity",
                    userId = user.Id,
                    code,
                    returnUrl = returnPath
                },
                protocol: Request.Scheme);

            await emailSender.SendEmailAsync(Input.Email,
                "Confirm your email",
                $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl!)}'>clicking here</a>.");

            if (userManager.Options.SignIn.RequireConfirmedAccount)
            {
                return RedirectToPage("RegisterConfirmation",
                    new
                    {
                        email = Input.Email,
                        returnUrl = returnPath
                    });
            }
            else
            {
                await signInManager.SignInAsync(user, isPersistent: false);
                return LocalRedirect(returnPath);
            }
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        // If we got this far, something failed, redisplay form
        return Page();
    }

    public sealed class InputModel
    {
        [Required]
        [Display(Name = "Username")]
        public string Username { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        [StringLength(100,
            ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.",
            MinimumLength = Constants.MinimumPasswordLength)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
    }
}
