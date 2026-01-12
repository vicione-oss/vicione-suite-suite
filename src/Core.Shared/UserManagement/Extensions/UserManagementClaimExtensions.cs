using System.Security.Claims;
using Sdk.UserManagement.Contracts;

namespace Core.Shared.UserManagement.Extensions;

public static class UserManagementClaimExtensions
{
    public static Claim ToClaim(this UserManagementClaim claim)
        => new(claim.Type, claim.Value);
}
