using Blazor.Shared.UserManagement.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;

namespace Blazor.Server.Backend.Services;

internal class LanguageCookieReader(IHttpContextAccessor httpContextAccessor) : ILanguageCookieReader
{
    public string GetCookieValue()
    {
        if (httpContextAccessor.HttpContext is null ||
            !httpContextAccessor.HttpContext.Request.Cookies.TryGetValue(CookieRequestCultureProvider.DefaultCookieName, out var value))
        {
            return string.Empty;
        }

        return value;
    }
}
