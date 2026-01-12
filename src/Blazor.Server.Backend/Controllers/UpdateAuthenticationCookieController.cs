using System.Net;
using Core.Shared.Instance.Services;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Blazor.Server.Backend.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed partial class UpdateAuthenticationCookieController(SignInManager<SuiteUser> signInManager, UserManager<SuiteUser> userManager,
    INonceStore nonceStore, ILogger<UpdateAuthenticationCookieController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> Update([FromQuery(Name = "nonce")] Guid nonceValue)
    {
        var user = await userManager.GetUserAsync(HttpContext.User);
        if (user is null)
            return Unauthorized();

        var nonce = await nonceStore.GetNonce(nonceValue);
        if (nonce is null)
            return Unauthorized();

        await nonceStore.Delete(nonce);

        await signInManager.RefreshSignInAsync(user);

        AuthenticationCookieUpdated(logger);

        return StatusCode((int)HttpStatusCode.NoContent);
    }

    [LoggerMessage(1, LogLevel.Information, "Authentication cookie updated")]
    private static partial void AuthenticationCookieUpdated(ILogger<UpdateAuthenticationCookieController> logger);
}
