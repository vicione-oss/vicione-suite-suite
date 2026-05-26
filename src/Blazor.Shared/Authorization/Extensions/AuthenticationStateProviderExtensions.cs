using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Blazor.Shared.Authorization.Extensions;

public static class AuthenticationStateProviderExtensions
{
    extension(AuthenticationStateProvider authenticationStateProvider)
    {
        public async Task<ClaimsPrincipal?> GetUser()
        {
            var authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync();

            return authenticationState.User;
        }

        public async Task<string?> GetUserName()
        {
            var authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync();

            return authenticationState.GetUserName();
        }
    }
}
