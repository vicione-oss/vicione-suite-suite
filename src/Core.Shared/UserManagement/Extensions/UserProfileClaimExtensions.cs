using System.Security.Claims;
using Core.Shared.UserManagement.Contracts;

namespace Core.Shared.UserManagement.Extensions;

public static class UserProfileClaimExtensions
{
    public static Claim ToClaim(this UserProfileClaim claim)
        => new(claim.Type, claim.Value);
}
