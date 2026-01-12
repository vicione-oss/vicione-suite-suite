using Blazor.Shared.UserManagement.Models;
using Core.Shared.UserManagement.Extensions;
using Sdk.Authorization;
using Sdk.UserManagement.Contracts;

namespace Blazor.Shared.UserManagement.ControlPanels.Role.Services;

internal class GridItemService(IModuleAuthorizationClaimParser moduleAuthorizationClaimParser) : IGridItemService
{
    public void UpdateGridItems(RoleControlPanelState state)
    {
        var gridItems = GetPermissionGridItems(state)
            .OrderBy(pgi => pgi.ModuleId)
            .ThenBy(pgi => pgi.Feature)
            .ToList();

        gridItems = [.. gridItems.Where(gi =>
            gi.ModuleId.Contains(state.FilterText, StringComparison.OrdinalIgnoreCase)
            || gi.Feature.Contains(state.FilterText, StringComparison.OrdinalIgnoreCase))];

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

    private List<PermissionGridItem> GetPermissionGridItems(RoleControlPanelState state)
    {
        var assignedClaims = new Dictionary<UserManagementClaim, ModuleAuthorizationClaimValue>();
        var availableClaims = new Dictionary<UserManagementClaim, ModuleAuthorizationClaimValue>();

        if (state.Role is not null)
        {
            foreach (var claim in state.Role.Claims)
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
}

