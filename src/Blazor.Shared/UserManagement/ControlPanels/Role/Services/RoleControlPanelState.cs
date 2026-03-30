using Blazor.Shared.UserManagement.Models;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Services;
using Sdk.UserManagement.Contracts;

namespace Blazor.Shared.UserManagement.ControlPanels.Role.Services;

public sealed class RoleControlPanelState : ControlPanelState
{
    private string? _roleName;

    internal Sdk.UserManagement.Contracts.Role? Role { get; set; }

    internal PermissionEditContext? PermissionEditContext { get; set; }

    internal IEnumerable<UserManagementClaim> AvailableClaims { get; set; } = [];
    internal IEnumerable<IModuleFeature> Features { get; set; } = [];
    internal string? FilterText { get; set; }
    internal IQueryable<PermissionGridItem> FilteredGridItems { get; set; } = Enumerable.Empty<PermissionGridItem>().AsQueryable();
    internal bool IsEditMode => !string.IsNullOrEmpty(_roleName);

    /// <remarks>
    /// <see cref="RoleControlPanel"/> will use this property to decide whether a role should be edited (value is set) or created (value is null)
    /// </remarks>
    public string? RoleName
    {
        get => _roleName;
        set
        {
            if (value != _roleName)
            {
                _roleName = value;

                OnPropertyChanged();
            }
        }
    }

    public bool RoleSettingsGroupExpanded { get; set; } = true;
}
