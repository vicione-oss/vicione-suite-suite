using System.Security.Claims;
using Sdk.UserManagement.Contracts;

namespace Core.Shared.UserManagement.Extensions;

public static class ClaimExtensions
{
    public static UserManagementClaim ToUserManagementClaim(this Claim claim)
        => new() { Type = claim.Type, Value = claim.Value };
}
