using System.Text;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Account;

public class ConfirmEmail(UserManager<SuiteUser> userManager) : PageModel
{
    public bool WasSuccessful { get; set; }

    public async Task<IActionResult> OnGetAsync(string userId, string code)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
        {
            return RedirectToPage("/Account/Login");
        }
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userId}'.");
        }

        code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        var result = await userManager.ConfirmEmailAsync(user, code);
        WasSuccessful = result.Succeeded;

        return Page();
    }
}
