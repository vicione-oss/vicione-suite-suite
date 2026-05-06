using System.Security.Claims;

namespace Core.Shared.Authorization.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static string? GetUserName(this ClaimsPrincipal? claimsPrincipal)
        => claimsPrincipal?.Identity?.Name;
}
