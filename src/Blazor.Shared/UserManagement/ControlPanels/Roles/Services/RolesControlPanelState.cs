using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.UserManagement.ControlPanels.Roles.Services;

public sealed class RolesControlPanelState : ControlPanelState
{
    private List<Sdk.UserManagement.Contracts.Role> _roles = [];
    private bool _resetSelectedRoles;

    internal List<Sdk.UserManagement.Contracts.Role> Roles
    {
        get => _roles;
        set
        {
            if (value == _roles)
                return;

            _roles = value;

            OnPropertyChanged(nameof(Roles));
        }
    }

    internal bool ResetSelectedRoles
    {
        get => _resetSelectedRoles;
        set
        {
            if (value == _resetSelectedRoles)
                return;

            _resetSelectedRoles = value;

            OnPropertyChanged(nameof(ResetSelectedRoles));
        }
    }

    internal List<Sdk.UserManagement.Contracts.Role> DeletingRoles { get; } = [];
}
