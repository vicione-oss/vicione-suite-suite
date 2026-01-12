using System.Security.Claims;
using Sdk.Authorization;

namespace Blazor.Shared.UserManagement.ControlPanels.User.Services;

internal sealed class ClaimsProvider(IEnumerable<IModuleFeature> features) : IClaimsProvider
{
    public Task<IEnumerable<Claim>> GetClaims()
        => Task.FromResult(features.Select(f => ModuleAuthorizationClaimFactory.CreateClaim(f.ModuleId, AccessLevel.Partial, f.Name)));
}
