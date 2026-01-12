using System.Security.Claims;
using Core.Shared.UserManagement.Contracts;

namespace Core.Shared.UserManagement.Extensions;

public static class ClaimExtensions
{
    public static UserProfileClaim ToUserProfileClaim(this Claim claim)
        => new() { Type = claim.Type, Value = claim.Value };
}
