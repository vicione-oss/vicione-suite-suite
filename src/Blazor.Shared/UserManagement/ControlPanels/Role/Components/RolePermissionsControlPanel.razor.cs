using Blazor.Shared.Services;
using Blazor.Shared.UserManagement.ControlPanels.Role.Services;
using Blazor.Shared.UserManagement.ControlPanels.Roles.Models;
using Blazor.Shared.UserManagement.Models;
using Core.Shared.UserManagement.Extensions;
using Microsoft.AspNetCore.Components;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using ViciOne.Ui.Blazor.Components.Grid.Services;

namespace Blazor.Shared.UserManagement.ControlPanels.Role.Components;

[ControlPanelCategory<ControlPanelCategoryDescriptor>]
[ControlPanelGroup<SecondaryControlPanelGroupDescriptor>]
[ControlPanelChild<RoleControlPanelDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class RolePermissionsControlPanel : ControlPanelBase<RoleControlPanelState>
{
    private bool _deletePermissionActionButtonEnabled;

    [Inject] private IGridItemService GridItemService { get; set; } = default!;
    [Inject(Key = typeof(RolesControlPanelServiceKey))] private IGridItemSelection<PermissionGridItemId> PermissionsGridItemSelection { get; set; } = default!;

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

    protected override async ValueTask DisposeAsyncCore()
    {
        PermissionsGridItemSelection.Changed -= PermissionsGridItemSelectionChanged;

        await base.DisposeAsyncCore();
    }

    private async Task AccessLevelChangedAsync(PermissionGridItem gridItem)
    {
        if (State.Role is null)
            return;

        if (gridItem.Claim is not null) // remove old claim to prevent duplicates
            State.Role.Claims.Remove(gridItem.Claim.Value);

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
            State.Role.Claims.Add(gridItem.Claim.Value);
        }

        await BeginEdit();
    }
}
