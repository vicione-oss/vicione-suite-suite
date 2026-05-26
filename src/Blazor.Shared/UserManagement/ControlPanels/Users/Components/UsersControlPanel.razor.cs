using Blazor.Shared.Services;
using Blazor.Shared.UserManagement.ControlPanels.User.Components;
using Blazor.Shared.UserManagement.ControlPanels.User.Services;
using Blazor.Shared.UserManagement.ControlPanels.Users.Models;
using Blazor.Shared.UserManagement.ControlPanels.Users.Services;
using Blazor.Shared.UserManagement.Services;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Services;
using Sdk.Messaging;
using ViciOne.Ui.Blazor.Components.Grid.Services;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.UserManagement.ControlPanels.Users.Components;

[ControlPanelCategory<ControlPanelCategoryDescriptor>]
[ControlPanelGroup<SecondaryControlPanelGroupDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class UsersControlPanel : ControlPanelBase<UsersControlPanelState>
{
    private readonly string _descriptionBannerIconCssClass = MonochromeIconName.UserLight.GetCssClasses().ToSpaceSeparated();

    private string? _identityName;
    private IQueryable<UserProfile>? _usersQueryable;
    private string? _filterText;
    private bool _isSingleUserSelected;
    private bool _deleteGridActionButtonEnabled;

    [Inject] private IControlPanelRequest ControlPanelRequest { get; set; } = default!;

    [Inject] private IUserService UserService { get; set; } = default!;

    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

    [Inject(Key = typeof(UsersControlPanelServiceKey))] private IGridItemSelection<UserName> UserGridItemSelection { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        State.ResetSelectedUsers = false;

        State.Changed += StateChanged;
        UserGridItemSelection.Changed += UserGridItemSelectionChanged;

        UpdateStatesDependingOnUserGridItemSelection();

        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        _identityName = authState.User.Identity?.Name;

        UserService.UserChanged += UserChanged;

        UpdateUsersQueryable();
    }

    private async void StateChanged(ControlPanelStateChangedEventArgs obj)
    {
        var reload = false;

        if (obj.PropertyNames.Contains(nameof(State.ResetSelectedUsers)) && State.ResetSelectedUsers)
        {
            UserGridItemSelection.BeginUpdate();

            try
            {
                foreach (var user in State.DeletingUsers)
                    UserGridItemSelection.Add(user.UserName);
            }
            finally
            {
                UserGridItemSelection.EndUpdate();
            }

            State.ResetSelectedUsers = false;

            reload = true;
        }

        if (obj.PropertyNames.Contains(nameof(State.Users)))
        {
            UpdateUsersQueryable();
            UpdateStatesDependingOnUserGridItemSelection();

            reload = true;
        }

        if (reload)
            await InvokeAsync(StateHasChanged);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        State.Changed += StateChanged;

        UserService.UserChanged -= UserChanged;

        UserGridItemSelection.Changed -= UserGridItemSelectionChanged;

        await base.DisposeAsyncCore();
    }

    private void DeleteSelectedUsers()
    {
        UserGridItemSelection.BeginUpdate();

        try
        {
            foreach (var userName in UserGridItemSelection)
            {
                var users = State.Users.Where(u => u.UserName == userName && u.UserName.Value != _identityName).ToArray();

                if (!users.Any())
                    continue;

                State.DeletingUsers.AddRange(users);
                UserGridItemSelection.Remove(users.First().UserName);
                State.Users.Remove(users.First());
            }
        }
        finally
        {
            UserGridItemSelection.EndUpdate();
        }

        UpdateUsersQueryable();
        BeginEdit();
    }

    private async Task UserChanged(UserProfile userProfile, CrudAction crudAction)
    {
        if (crudAction == CrudAction.Created)
            await UserCreated(userProfile);
        else if (crudAction == CrudAction.Updated)
            await UserUpdated(userProfile);
        else if (crudAction == CrudAction.Deleted)
            await UserDeleted(userProfile);
    }

    private async Task UserCreated(UserProfile userProfile)
    {
        State.Users.Add(userProfile);

        UpdateUsersQueryable();

        await InvokeAsync(StateHasChanged);
    }

    private async Task UserUpdated(UserProfile userProfile)
    {
        var userName = userProfile.UserName;

        var userIndex = State.Users.FindIndex(u => u.UserName == userName);
        if (userIndex != -1)
        {
            State.Users[userIndex] = userProfile;

            UpdateUsersQueryable();

            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task UserDeleted(UserProfile userProfile)
    {
        var userName = userProfile.UserName;

        State.Users = [.. State.Users.Where(u => u.UserName != userName)];

        UserGridItemSelection.Remove(userName);
        UpdateUsersQueryable();

        await InvokeAsync(StateHasChanged);
    }


    private async void UserGridItemSelectionChanged(GridItemSelectionChangedEventArgs<UserName> args)
    {
        if (UpdateStatesDependingOnUserGridItemSelection())
            await InvokeAsync(StateHasChanged);
    }

    private bool UpdateStatesDependingOnUserGridItemSelection()
    {
        var somethingChanged = false;

        var isSingleUserSelected = UserGridItemSelection.Count == 1;

        if (isSingleUserSelected != _isSingleUserSelected)
        {
            _isSingleUserSelected = isSingleUserSelected;
            somethingChanged = true;
        }

        var deleteGridActionButtonEnabled = false;
        if (UserGridItemSelection.Count > 0)
        {
            deleteGridActionButtonEnabled = UserGridItemSelection.Any(username => username.Value != _identityName);
        }

        if (deleteGridActionButtonEnabled != _deleteGridActionButtonEnabled)
        {
            _deleteGridActionButtonEnabled = deleteGridActionButtonEnabled;
            somethingChanged = true;
        }

        return somethingChanged;
    }

    private async Task AddUser()
        => await ControlPanelRequest.Send<UserControlPanel, UserControlPanelState>(
            s => s.UserName = null);

    private async Task EditSelectedUser()
        => await ControlPanelRequest.Send<UserControlPanel, UserControlPanelState>(
            s => s.UserName = UserGridItemSelection.FirstOrDefault());

    private async Task EditUser(UserProfile userProfile)
        => await ControlPanelRequest.Send<UserControlPanel, UserControlPanelState>(s => s.UserName = userProfile.UserName);

    private void ApplyFilter()
        => UpdateUsersQueryable();

    private void UpdateUsersQueryable()
    {
        _usersQueryable = State.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(_filterText))
        {
            _usersQueryable = _usersQueryable.Where(u => u.UserName.Value.Contains(_filterText, StringComparison.CurrentCultureIgnoreCase) ||
                (u.Email != null && u.Email.Contains(_filterText, StringComparison.CurrentCultureIgnoreCase)));
        }

        _usersQueryable = _usersQueryable.OrderBy(u => u.UserName);
    }
}
