using System.Globalization;
using System.Security.Claims;
using Blazor.Shared.UserManagement.Models;
using Blazor.Shared.UserManagement.Services;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Extensions;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Services;
using Sdk.UserManagement.Contracts;

namespace Blazor.Shared.UserManagement.ControlPanels.User.Services;

public sealed class UserControlPanelState(IModuleAuthorizationClaimParser moduleAuthorizationClaimParser, IClaimsProvider claimsProvider) : ControlPanelState
{

    private UserName? _userName;

    internal UserProfile? UserProfile { get; set; }
    internal CultureInfo? SelectedCulture { get; set; }
    internal TimeZoneInfo? SelectedTimeZone { get; set; }
    internal string? CurrentPassword { get; set; }
    internal string? NewPassword { get; set; }
    internal string? RepeatNewPassword { get; set; }
    internal string? PasswordExpirationDateString { get; set; }

    internal PermissionEditContext? PermissionEditContext { get; set; }

    private IEnumerable<UserManagementClaim> AvailableClaims { get; set; } = [];
    internal IEnumerable<string>? AvailableUserRoles { get; set; }
    internal IEnumerable<string>? AvailableRoles { get; set; }
    internal IEnumerable<IModuleFeature> Features { get; set; } = [];
    internal string FilterText { get; set; } = string.Empty;
    internal IQueryable<PermissionGridItem> FilteredGridItems { get; set; } = Enumerable.Empty<PermissionGridItem>().AsQueryable();

    internal string RoleToAdd { get; set; } = string.Empty;

    /// <remarks>
    /// <see cref="UserControlPanel"/> will use this property to decide whether a user should be edited (value is set) or created (value is null)
    /// </remarks>
    public UserName? UserName
    {
        get => _userName;
        set
        {
            if (value != _userName)
            {
                _userName = value;

                OnPropertyChanged();
            }
        }
    }

    public bool UserSettingsGroupExpanded { get; set; } = true;
    public bool ContactSettingsGroupExpanded { get; set; } = true;
    public bool PasswordSettingsGroupExpanded { get; set; } = true;
    public bool CompanySettingsGroupExpanded { get; set; } = true;
    public bool LocalizationSettingsGroupExpanded { get; set; } = true;

    internal bool IsEditMode()
        => !string.IsNullOrEmpty(UserName?.Value);

    internal IEnumerable<PermissionGridItem> GetPermissionGridItems()
    {
        var assignedClaims = new Dictionary<UserManagementClaim, ModuleAuthorizationClaimValue>();
        var availableClaims = new Dictionary<UserManagementClaim, ModuleAuthorizationClaimValue>();

        if (UserProfile is not null)
        {
            foreach (var claim in UserProfile.Claims)
            {
                if (moduleAuthorizationClaimParser.TryParse(claim.ToClaim(), out var modAuthClaimVal))
                    assignedClaims.Add(claim, modAuthClaimVal);
            }
        }

        foreach (var claim in AvailableClaims)
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
                    Description = Features
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
                    Description = Features
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
        => AvailableUserRoles = AvailableRoles?.Except(UserProfile?.Roles ?? []);

    internal async Task UpdateAvailableClaims()
    {
        var allClaims = await claimsProvider.GetClaims();
        var claimItems = GetClaims(allClaims);

        var availableClaims = new List<UserManagementClaim>();
        foreach (var (_, claimValue) in claimItems)
        {
            availableClaims.Add(
                ModuleAuthorizationClaimFactory
                    .CreateClaim(claimValue.ModuleId, AccessLevel.Partial, claimValue.FeatureName)
                    .ToUserManagementClaim()
            );
        }

        AvailableClaims = availableClaims;
    }

    private Dictionary<string, ModuleAuthorizationClaimValue> GetClaims(IEnumerable<Claim> claims)
    {
        var result = new Dictionary<string, ModuleAuthorizationClaimValue>();
        foreach (var claim in claims)
        {
            if (moduleAuthorizationClaimParser.TryParse(claim, out var claimValue))
                result.Add($"{claimValue.ModuleId} - {claimValue.FeatureName}", claimValue);
        }
        return result;
    }

    internal void UpdateGridItems()
    {
        var gridItems = GetPermissionGridItems()
            .OrderBy(pgi => pgi.ModuleId)
            .ThenBy(pgi => pgi.Feature)
            .ToList();

        gridItems = [.. gridItems.Where(gi =>
            gi.ModuleId.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
            || gi.Feature.Contains(FilterText, StringComparison.OrdinalIgnoreCase))];

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

        FilteredGridItems = gridItems.AsQueryable();
    }
}
