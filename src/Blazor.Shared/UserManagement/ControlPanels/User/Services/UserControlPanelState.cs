using System.Globalization;
using Blazor.Shared.UserManagement.Models;
using Core.Shared.UserManagement.Contracts;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Services;
using Sdk.UserManagement.Contracts;

namespace Blazor.Shared.UserManagement.ControlPanels.User.Services;

public sealed class UserControlPanelState : ControlPanelState
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

    internal IEnumerable<UserManagementClaim> AvailableClaims { get; set; } = [];
    internal IEnumerable<string>? AvailableUserRoles { get; set; }
    internal IEnumerable<string>? AvailableRoles { get; set; }
    internal IEnumerable<IModuleFeature> Features { get; set; } = [];
    internal string? FilterText { get; set; }
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
}
