using Core.Shared.Authorization.Extensions;
using Microsoft.AspNetCore.Components.Authorization;

namespace Blazor.Shared.Authorization.Extensions;

public static class AuthenticationStateExtensions
{
    public static string? GetUserName(this AuthenticationState? authenticationState)
        => authenticationState?.User.GetUserName();
}
