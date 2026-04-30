using System.Security.Claims;
using Blazor.Shared.UserManagement.ControlPanels.User.Services;
using Blazor.Shared.UserManagement.Models;
using Blazor.Shared.UserManagement.Services;
using Core.Shared.UserManagement.Extensions;
using Sdk.Authorization;
using Sdk.UserManagement.Contracts;

namespace Blazor.Shared.UserManagement.ControlPanels.User.Extensions;

internal static class UserControlPanelStateExtensions
{
    extension(UserControlPanelState state)
    {
        internal IEnumerable<PermissionGridItem> GetPermissionGridItems(IModuleAuthorizationClaimParser moduleAuthorizationClaimParser)
        {
            var assignedClaims = new Dictionary<UserManagementClaim, ModuleAuthorizationClaimValue>();
            var availableClaims = new Dictionary<UserManagementClaim, ModuleAuthorizationClaimValue>();

            if (state.UserProfile is not null)
            {
                foreach (var claim in state.UserProfile.Claims)
                {
                    if (moduleAuthorizationClaimParser.TryParse(claim.ToClaim(), out var modAuthClaimVal))
                        assignedClaims.Add(claim, modAuthClaimVal);
                }
            }

            foreach (var claim in state.AvailableClaims)
            {
                if (moduleAuthorizationClaimParser.TryParse(claim.ToClaim(), out var modAuthClaimVal))
                    availableClaims.Add(claim, modAuthClaimVal);
            }

            var gridItems = new List<PermissionGridItem>();
            foreach (var (availClaim, availModAuthValClaimVal) in availableClaims)
            {
                var assigned = assignedClaims
                    .Where(assCl => assCl.Value.ModuleId == availModAuthValClaimVal.ModuleId && assCl.Value.FeatureName == availModAuthValClaimVal.FeatureName)
                    .ToArray();
                if (assigned.Length != 0)
                {
                    var assignedClaim = assigned[0].Value;
                    gridItems.Add(new PermissionGridItem
                    {
                        AccessLevel = assignedClaim.AccessLevel == AccessLevel.Partial ? PermissionGridAccessLevel.Partial : PermissionGridAccessLevel.Full,
                        Claim = assigned[0].Key,
                        Description = state.Features
                            .FirstOrDefault(f => f.ModuleId == assignedClaim.ModuleId && f.Name == assignedClaim.FeatureName)?
                            .Description ?? string.Empty,
                        Feature = assignedClaim.FeatureName,
                        ModuleId = assignedClaim.ModuleId,
                    });
                }
                else
                {
                    gridItems.Add(new PermissionGridItem
                    {
                        AccessLevel = PermissionGridAccessLevel.None,
                        Claim = availClaim,
                        Description = state.Features
                            .FirstOrDefault(f => f.ModuleId == availModAuthValClaimVal.ModuleId && f.Name == availModAuthValClaimVal.FeatureName)?
                            .Description ?? string.Empty,
                        Feature = availModAuthValClaimVal.FeatureName,
                        ModuleId = availModAuthValClaimVal.ModuleId,
                    });
                }
            }

            return gridItems;
        }

        internal void UpdateAvailableUserRoles()
            => state.AvailableUserRoles = state.AvailableRoles?.Except(state.UserProfile?.Roles ?? []);

        internal async Task UpdateAvailableClaims(IModuleAuthorizationClaimParser moduleAuthorizationClaimParser, IClaimsProvider claimsProvider)
        {
            var allClaims = await claimsProvider.GetClaims();
            var claimItems = GetClaims(moduleAuthorizationClaimParser, allClaims);

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

        internal void UpdateGridItems(IModuleAuthorizationClaimParser moduleAuthorizationClaimParser)
        {
            var gridItems = state.GetPermissionGridItems(moduleAuthorizationClaimParser)
                .OrderBy(pgi => pgi.ModuleId)
                .ThenBy(pgi => pgi.Feature)
                .ToList();

            if (!string.IsNullOrEmpty(state.FilterText))
            {
                gridItems = [.. gridItems.Where(gi =>
            gi.ModuleId.Contains(state.FilterText, StringComparison.OrdinalIgnoreCase)
            || gi.Feature.Contains(state.FilterText, StringComparison.OrdinalIgnoreCase))];
            }

            var module = "";
            for (var i = 0; i < gridItems.Count; i++)
            {
                var gridItem = gridItems[i];
                if (gridItem.ModuleId == module)
                    continue;

                module = gridItem.ModuleId;

                // insert grouping row for new module
                gridItems.Insert(i, new PermissionGridItem
                {
                    AccessLevel = PermissionGridAccessLevel.None,
                    Claim = null,
                    Description = string.Empty,
                    Feature = string.Empty,
                    IsGroupingRow = true,
                    ModuleId = gridItem.ModuleId,
                });
            }

            state.FilteredGridItems = gridItems.AsQueryable();
        }
    }

    private static Dictionary<string, ModuleAuthorizationClaimValue> GetClaims(IModuleAuthorizationClaimParser moduleAuthorizationClaimParser, IEnumerable<Claim> claims)
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
