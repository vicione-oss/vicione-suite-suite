using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Account;

public class RegisterConfirmation : PageModel
{
    public Task<IActionResult> OnGetAsync() => Task.FromResult<IActionResult>(Page());
}
