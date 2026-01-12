using System.Security.Claims;
using Core.Shared.UserManagement.Contracts;

namespace Core.Shared.UserManagement.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static bool IsAssociatedWith(this ClaimsPrincipal? user, UserProfile userProfile)
        => string.Equals(user?.Identity?.Name, userProfile.UserName.Value, StringComparison.OrdinalIgnoreCase);
}
