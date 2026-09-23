using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace Core.OS.UserManagement.Configuration;

/// <summary>
/// Extends the default <see cref="AuthenticationSchemeProvider"/> to support dynamically resolved
/// authentication schemes — for example, OIDC providers loaded from the database at request time.
///
/// <para>
/// This class is currently a no-op placeholder. It exists to document the extension point for
/// supporting multiple OIDC providers in the future.
/// </para>
///
/// <para>
/// <b>How to extend:</b> Override <see cref="AuthenticationSchemeProvider.GetSchemeAsync"/> and, after the
/// <c>base.GetSchemeAsync</c> call returns <c>null</c>, query the database for a provider matching
/// <paramref name="name"/>. If found, return a new <see cref="AuthenticationScheme"/> using that
/// <paramref name="name"/> and <see cref="OpenIdConnectHandler"/> as the handler type. The name must
/// match the name used when configuring <see cref="Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions"/>
/// via <see cref="IConfigureNamedOptions{TOptions}"/>, so the options framework can resolve the
/// correct settings for that provider.
/// </para>
/// </summary>
public class DynamicAuthenticationSchemeProvider(
    IOptions<AuthenticationOptions> options) :
    AuthenticationSchemeProvider(options)
{
    // The base implementation handles every scheme registered today. Supporting multiple OIDC
    // providers means overriding GetSchemeAsync to look the provider up in ExternalIdProviders and
    // return an AuthenticationScheme bound to OpenIdConnectHandler.

    // IOptionsMonitor caches options by name after first access, so provider settings that can
    // change at runtime will need a cache invalidation strategy too.
}
