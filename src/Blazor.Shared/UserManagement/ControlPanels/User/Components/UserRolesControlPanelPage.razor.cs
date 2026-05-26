using Blazor.Shared.UserManagement.ControlPanels.User.Extensions;
using Blazor.Shared.UserManagement.ControlPanels.User.Models;
using Blazor.Shared.UserManagement.ControlPanels.User.Services;
using Microsoft.AspNetCore.Components;
using ViciOne.Ui.Blazor.Components.Grid.Services;

namespace Blazor.Shared.UserManagement.ControlPanels.User.Components;

public sealed partial class UserRolesControlPanelPage : ComponentBase, IAsyncDisposable
{
    private bool _deleteRoleActionButtonEnabled;

    private IQueryable<string>? _assignedUserRoles => State.UserProfile?.Roles
        .Order()
        .AsQueryable();

    [Parameter, EditorRequired]
    public UserControlPanelState State { get; set; } = default!;

    [Parameter, EditorRequired]
    public EventCallback OnEdit { get; set; }

    [Inject(Key = typeof(UserControlPanelServiceKey))]
    private IGridItemSelection<string> RolesGridItemSelection { get; set; } = default!;

    protected override void OnInitialized()
    {
        RolesGridItemSelection.Clear();
        RolesGridItemSelection.Changed += RolesGridItemSelectionChanged;
    }

    private async void RolesGridItemSelectionChanged(GridItemSelectionChangedEventArgs<string> obj)
    {
        var state = RolesGridItemSelection.Count > 0;
        if (state == _deleteRoleActionButtonEnabled)
            return;

        _deleteRoleActionButtonEnabled = state;
        await InvokeAsync(StateHasChanged);
    }

    private async Task AddToRole()
    {
        if (State.RoleToAdd is null)
            return;

        State.UserProfile?.Roles.Add(State.RoleToAdd);

        State.UpdateAvailableUserRoles();

        State.RoleToAdd = new(State.AvailableUserRoles?.FirstOrDefault() ?? string.Empty);

        await BeginEdit();
    }

    private async Task RemoveSelectedRoles()
    {
        if (RolesGridItemSelection.Count == 0)
            return;

        foreach (var role in RolesGridItemSelection)
            State.UserProfile?.Roles.Remove(role);

        State.UpdateAvailableUserRoles();

        if (State.AvailableRoles is not null && State.AvailableRoles.All(k => k != State.RoleToAdd))
        {
            State.RoleToAdd = new(State.AvailableUserRoles?.FirstOrDefault() ?? string.Empty);
        }

        RolesGridItemSelection.Clear();

        await BeginEdit();
    }

    private async Task BeginEdit()
    {
        if (OnEdit.HasDelegate)
            await OnEdit.InvokeAsync();
    }

    public ValueTask DisposeAsync()
    {
        RolesGridItemSelection.Changed -= RolesGridItemSelectionChanged;
        return ValueTask.CompletedTask;
    }
}
