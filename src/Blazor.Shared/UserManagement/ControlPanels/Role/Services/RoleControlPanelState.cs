using Blazor.Shared.UserManagement.Models;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Services;
using Sdk.UserManagement.Contracts;
using ViciOne.SourceGenerators.NotifyChanged;

namespace Blazor.Shared.UserManagement.ControlPanels.Role.Services;

public sealed partial class RoleControlPanelState : ControlPanelState
{
    internal Sdk.UserManagement.Contracts.Role? Role { get; set; }

    internal PermissionEditContext? PermissionEditContext { get; set; }

    internal IEnumerable<UserManagementClaim> AvailableClaims { get; set; } = [];
    internal IEnumerable<IModuleFeature> Features { get; set; } = [];
    internal string? FilterText { get; set; }
    internal IQueryable<PermissionGridItem> FilteredGridItems { get; set; } = Enumerable.Empty<PermissionGridItem>().AsQueryable();
    internal bool IsEditMode => !string.IsNullOrEmpty(RoleName);

    /// <remarks>
    /// <see cref="Components.RoleControlPanel"/> will use this property to decide whether a role should be edited (value is set) or created (value is null)
    /// </remarks>
    [NotifyChanged]
    public partial string? RoleName { get; set; }

    public bool RoleSettingsGroupExpanded { get; set; } = true;
}
