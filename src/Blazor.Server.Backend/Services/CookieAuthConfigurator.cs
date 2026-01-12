using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Blazor.Server.Backend.Services;

/// <summary>
/// Applies hardened cookie settings and sets session store if the auth schema matches
/// </summary>
internal sealed class CookieAuthConfigurator(ITicketStore store) : IPostConfigureOptions<CookieAuthenticationOptions>
{
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        if (!string.Equals(name, Core.Shared.Constants.AuthenticationSchema, StringComparison.Ordinal))
            return;

        // Security hardening
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);

        // Keep user ticket server-side - store needs to be singleton!
        options.SessionStore = store;
    }
}
