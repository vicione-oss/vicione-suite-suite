using System.Security.Claims;
using Core.Shared.Authorization.Extensions;
using Core.Shared.UserManagement.Contracts;

namespace Core.Shared.UserManagement.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static bool IsAssociatedWith(this ClaimsPrincipal? claimsPrincipal, UserProfile userProfile)
        => string.Equals(claimsPrincipal.GetUserName(), userProfile.UserName.Value, StringComparison.OrdinalIgnoreCase);
}
