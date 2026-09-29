using Core.OS.UserManagement.Configuration;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace Core.OS.UserManagement.Security;

/// <summary>
/// The origin a sign-in or account-linking form submission ends up at, for ADR-006's <c>form-action</c>.
/// </summary>
/// <remarks>
/// Read from the options the challenge is built from, so it names the redirect's target and follows a
/// change without a restart. ADR-006 records why it uses the authority, not the discovery document.
/// </remarks>
internal sealed class ExternalLoginFormActionOrigin(IOptionsMonitor<OpenIdConnectOptions> options)
{
    public string? Resolve(HttpContext context)
    {
        var provider = options.Get(DynamicExternalIdProviderOptions.OptionsName);

        if (provider.ClientId == Constants.UnconfiguredClient)
            return null;

        if (!Uri.TryCreate(provider.Authority, UriKind.Absolute, out var authority))
            return null;

        var origin = AsciiOriginOf(authority);

        return IsSuiteItself(origin, context) ? null : origin;
    }

    private static string AsciiOriginOf(Uri authority)
        => new UriBuilder(authority.Scheme, authority.IdnHost, authority.Port).Uri.GetLeftPart(UriPartial.Authority);

    private static bool IsSuiteItself(string origin, HttpContext context)
        => string.Equals(origin,
            $"{context.Request.Scheme}://{context.Request.Host.Value}",
            StringComparison.OrdinalIgnoreCase);
}
