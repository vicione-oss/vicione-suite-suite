using Core.Shared.UserManagement.Contracts;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.UserManagement.ControlPanels.Users.Services;

public sealed class UsersControlPanelState : ControlPanelState
{
    private List<UserProfile> _users = [];
    private bool _resetSelectedUsers;

    internal List<UserProfile> Users
    {
        get => _users;
        set
        {
            if (value == _users)
                return;

            _users = value;

            OnPropertyChanged(nameof(Users));
        }
    }

    internal bool ResetSelectedUsers
    {
        get => _resetSelectedUsers;
        set
        {
            if (value == _resetSelectedUsers)
                return;

            _resetSelectedUsers = value;

            OnPropertyChanged(nameof(ResetSelectedUsers));
        }
    }

    internal List<UserProfile> DeletingUsers { get; } = [];
}
