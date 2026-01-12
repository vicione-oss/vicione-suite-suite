using Blazor.Server.Backend.Extensions;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Account;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
public sealed class LogoutModel(SignInManager<SuiteUser> signInManager, IHttpContextAccessor httpContextAccessor,
                                ILogger<LogoutModel> logger) : PageModel
{
    private readonly SignInManager<SuiteUser> _signInManager = signInManager;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly ILogger<LogoutModel> _logger = logger;

    public async Task<IActionResult> OnPost(Uri? returnUrl = null)
    {
        // todo: support return url?
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out");

        var baseUrl = _httpContextAccessor.HttpContext?.Request.BaseUrl();

        var uriBuilder = new UriBuilder($"{baseUrl}account/login");

        return Redirect(uriBuilder.Uri.ToString());
    }
}
