using Core.OS.UserManagement.Configuration;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace Core.OS.UserManagement.Security;

/// <summary>
/// The origin a sign-in or account-linking form submission ends up at, for the <c>form-action</c>
/// directive of ADR-006's policy.
/// </summary>
/// <remarks>
/// Read from the options the challenge itself is built from, so the policy cannot name a different
/// provider than the redirect goes to, and a changed authority takes effect without a restart.
/// Deriving the origin from the authority rather than from the discovery document is a trade-off
/// ADR-006 records.
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

        var origin = authority.GetLeftPart(UriPartial.Authority);

        return IsSuiteItself(origin, context) ? null : origin;
    }

    private static bool IsSuiteItself(string origin, HttpContext context)
        => string.Equals(origin,
            $"{context.Request.Scheme}://{context.Request.Host.Value}",
            StringComparison.OrdinalIgnoreCase);
}
