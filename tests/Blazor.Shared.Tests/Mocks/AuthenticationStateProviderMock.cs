using System.Security.Claims;
using Blazor.Shared.Tests.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Sdk.Authorization;

namespace Blazor.Shared.Tests.Mocks;

internal sealed class AuthenticationStateProviderMock(string userName, params IEnumerable<ModuleAuthorizationClaim> moduleAuthorizationClaims) : AuthenticationStateProvider
{
    private Task<AuthenticationState> _authenticationStateTask = CreateAuthenticationStateTask(userName, moduleAuthorizationClaims);

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => _authenticationStateTask;

    private static Task<AuthenticationState> CreateAuthenticationStateTask(string userName, params IEnumerable<ModuleAuthorizationClaim> moduleAuthorizationClaims)
    {
        var claims = new List<Claim> { new(ClaimsIdentity.DefaultNameClaimType, userName) };

        foreach (var moduleAuthorizationClaim in moduleAuthorizationClaims)
            claims.Add(ModuleAuthorizationClaimFactory.CreateClaim(moduleAuthorizationClaim.ModuleId, moduleAuthorizationClaim.AccessLevel, moduleAuthorizationClaim.FeatureName));

        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims))));
    }

    public async Task<ClaimsPrincipal?> ChangeUser(string userName, params IEnumerable<ModuleAuthorizationClaim> moduleAuthorizationClaims)
    {
        _authenticationStateTask = CreateAuthenticationStateTask(userName, moduleAuthorizationClaims);

        NotifyAuthenticationStateChanged(_authenticationStateTask);

        var authenticationState = await _authenticationStateTask;

        return authenticationState.User;
    }
}
