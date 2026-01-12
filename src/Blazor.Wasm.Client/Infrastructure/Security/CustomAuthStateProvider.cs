using System.Security.Claims;
using Blazor.Wasm.Client.Infrastructure.Security.Contracts;
using Microsoft.AspNetCore.Components.Authorization;

namespace Blazor.Wasm.Client.Infrastructure.Security;

public sealed class CustomAuthStateProvider(IAuthApi authApi, ILogger<CustomAuthStateProvider> logger) : AuthenticationStateProvider
{
    private CurrentUser? _currentUser;
    private readonly IAuthApi _authApi = authApi;
    private readonly ILogger<CustomAuthStateProvider> _logger = logger;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var identity = new ClaimsIdentity();
        var userInfo = await GetCurrentUser();
        if (userInfo is { IsAuthenticated: true })
        {
            identity = new ClaimsIdentity(GetUserClaims(userInfo), "Server authentication");
        }

        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    private async Task<CurrentUser?> GetCurrentUser()
    {
        if (_currentUser is { IsAuthenticated: true }) return _currentUser;

        try
        {
            _currentUser = await _authApi.CurrentUserInfo();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to retrieve user");
        }

        return _currentUser;
    }

    private static List<Claim> GetUserClaims(CurrentUser user)
    {
        var claims = new List<Claim>();

        if (!string.IsNullOrEmpty(user.UserName))
            claims.Add(new Claim(ClaimTypes.Name, user.UserName));

        if (user.Claims is not null)
            claims.AddRange(user.Claims.Select(c => new Claim(c.Key, c.Value)));

        return claims;
    }

    public async Task Logout()
    {
        await _authApi.Logout();
        _currentUser = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task Login(LoginRequest loginParameters)
    {
        await _authApi.Login(loginParameters);

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
