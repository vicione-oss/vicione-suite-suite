using Core.Shared.Instance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Blazor.Server.Backend.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]/[action]")]
public sealed partial class UpdateLanguageCookieController(INonceStore nonceStore,
    ILogger<UpdateLanguageCookieController> logger) : ControllerBase
{
    public async Task<ActionResult> Update([FromQuery(Name = "language")] string language, [FromQuery(Name = "nonce")] Guid nonceValue,
        string? returnRoute = null)
    {
        var nonce = await nonceStore.GetNonce(nonceValue);
        if (nonce is null)
            return Unauthorized();

        await nonceStore.Delete(nonce);

        HttpContext.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new(language)));

        LanguageCookieUpdated(logger, language);

        return LocalRedirect(returnRoute ?? Url.Content("~/"));
    }

    public async Task<ActionResult> Remove([FromQuery(Name = "nonce")] Guid nonceValue, string? returnRoute = null)
    {
        var nonce = await nonceStore.GetNonce(nonceValue);
        if (nonce is null)
            return Unauthorized();

        await nonceStore.Delete(nonce);

        HttpContext.Response.Cookies.Delete(CookieRequestCultureProvider.DefaultCookieName);

        LanguageCookieRemoved(logger);

        return LocalRedirect(returnRoute ?? Url.Content("~/"));
    }

    [LoggerMessage(1, LogLevel.Information, "Language cookie updated with language '{Language}'")]
    private static partial void LanguageCookieUpdated(ILogger<UpdateLanguageCookieController> logger, string Language);

    [LoggerMessage(2, LogLevel.Information, "Language cookie removed")]
    private static partial void LanguageCookieRemoved(ILogger<UpdateLanguageCookieController> logger);
}
