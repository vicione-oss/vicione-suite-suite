using Blazor.Shared.UserManagement.ControlPanels.User.Models;
using Blazor.Shared.UserManagement.ControlPanels.User.Services;
using Blazor.Shared.UserManagement.Models;
using Core.Shared.UserManagement.Extensions;
using Microsoft.AspNetCore.Components;
using Sdk.Authorization;
using ViciOne.Ui.Blazor.Components.Grid.Services;

namespace Blazor.Shared.UserManagement.ControlPanels.User.Components;

public sealed partial class UserPermissionsControlPanelPage : ComponentBase, IAsyncDisposable
{
    private bool _deletePermissionActionButtonEnabled;

    [Parameter, EditorRequired]
    public UserControlPanelState State { get; set; } = default!;

    [Parameter, EditorRequired]
    public EventCallback OnEdit { get; set; }

    [Inject(Key = typeof(UserControlPanelServiceKey))]
    private IGridItemSelection<PermissionGridItemId> PermissionsGridItemSelection { get; set; } = default!;

    protected override void OnInitialized()
    {
        PermissionsGridItemSelection.Clear();
        PermissionsGridItemSelection.Changed += PermissionsGridItemSelectionChanged;
    }

    private async void PermissionsGridItemSelectionChanged(GridItemSelectionChangedEventArgs<PermissionGridItemId> args)
    {
        var deletePermissionActionButtonEnabled = args.Sender.Count > 0;

        if (deletePermissionActionButtonEnabled != _deletePermissionActionButtonEnabled)
        {
            _deletePermissionActionButtonEnabled = deletePermissionActionButtonEnabled;

            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task AccessLevelChangedAsync(PermissionGridItem gridItem)
    {
        if (State.UserProfile is null)
            return;

        if (gridItem.Claim is not null) // remove old claim to prevent duplicates
            State.UserProfile.Claims.Remove(gridItem.Claim.Value);

        if (gridItem.AccessLevel == PermissionGridAccessLevel.None)
        {
            gridItem.Claim = null;
        }
        else
        {
            gridItem.Claim = ModuleAuthorizationClaimFactory
                .CreateClaim(gridItem.ModuleId,
                    gridItem.AccessLevel == PermissionGridAccessLevel.Partial ? AccessLevel.Partial : AccessLevel.Full,
                    gridItem.Feature)
                .ToUserManagementClaim();
            State.UserProfile.Claims.Add(gridItem.Claim.Value);
        }

        await BeginEdit();
    }

    private async Task BeginEdit()
    {
        if (OnEdit.HasDelegate)
            await OnEdit.InvokeAsync();
    }

    public ValueTask DisposeAsync()
    {
        PermissionsGridItemSelection.Changed -= PermissionsGridItemSelectionChanged;
        return ValueTask.CompletedTask;
    }
}
