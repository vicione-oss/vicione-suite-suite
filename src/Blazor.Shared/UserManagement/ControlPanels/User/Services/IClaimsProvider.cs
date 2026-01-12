using System.Security.Claims;

namespace Blazor.Shared.UserManagement.ControlPanels.User.Services;

/// <summary>
/// Provider for claims
/// </summary>
public interface IClaimsProvider
{
    Task<IEnumerable<Claim>> GetClaims();
}
