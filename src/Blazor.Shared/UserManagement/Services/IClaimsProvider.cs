using System.Security.Claims;

namespace Blazor.Shared.UserManagement.Services;

/// <summary>
/// Provider for claims
/// </summary>
public interface IClaimsProvider
{
    Task<IEnumerable<Claim>> GetClaims();
}
