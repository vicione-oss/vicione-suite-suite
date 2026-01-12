using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Blazor.Shared.Authorization.Extensions;

internal static class AuthenticationStateProviderExtensions
{
    public static async Task<ClaimsPrincipal?> GetUser(this AuthenticationStateProvider authenticationStateProvider)
    {
        var authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync();

        return authenticationState?.User;
    }
}
