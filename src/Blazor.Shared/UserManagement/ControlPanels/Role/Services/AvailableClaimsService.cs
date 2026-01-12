using System.Security.Claims;
using Blazor.Shared.UserManagement.Services;
using Core.Shared.UserManagement.Extensions;
using Sdk.Authorization;
using Sdk.UserManagement.Contracts;

namespace Blazor.Shared.UserManagement.ControlPanels.Role.Services;

internal class AvailibleClaimsService(IClaimsProvider claimsProvider, IModuleAuthorizationClaimParser moduleAuthorizationClaimParser) : IAvailableClaimsService
{
    public async Task UpdateAvailableClaims(RoleControlPanelState state)
    {
        var allClaims = await claimsProvider.GetClaims();
        var claimItems = GetClaims(allClaims, moduleAuthorizationClaimParser);

        var availableClaims = new List<UserManagementClaim>();
        foreach (var (_, claimValue) in claimItems)
        {
            availableClaims.Add(
                ModuleAuthorizationClaimFactory
                    .CreateClaim(claimValue.ModuleId, AccessLevel.Partial, claimValue.FeatureName)
                    .ToUserManagementClaim()
            );
        }

        state.AvailableClaims = availableClaims;
    }

    private static Dictionary<string, ModuleAuthorizationClaimValue> GetClaims(IEnumerable<Claim> claims, IModuleAuthorizationClaimParser moduleAuthorizationClaimParser)
    {
        var result = new Dictionary<string, ModuleAuthorizationClaimValue>();
        foreach (var claim in claims)
        {
            if (moduleAuthorizationClaimParser.TryParse(claim, out var claimValue))
                result.Add($"{claimValue.ModuleId} - {claimValue.FeatureName}", claimValue);
        }

        return result;
    }
}
