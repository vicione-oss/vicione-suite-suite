using Microsoft.AspNetCore.Http;

namespace Blazor.Wasm.Backend.Controllers;

public interface ICookieAccessor
{
    bool HasRequestCookie(HttpContext context, string cookieName);
}

/// <summary>
/// found no way to inject the cookie in the test cases :(
/// </summary>
public sealed class CookieAccessor : ICookieAccessor
{
    public bool HasRequestCookie(HttpContext context, string cookieName)
    {
        return context.Request.Cookies.ContainsKey(cookieName);
    }
}
