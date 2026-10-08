using Blazor.Shared.Services;
using Blazor.Shared.UserManagement.ControlPanels.Role.Services;
using Blazor.Shared.UserManagement.ControlPanels.Roles.Models;
using Blazor.Shared.UserManagement.Models;
using Core.Shared.UserManagement.Extensions;
using Microsoft.AspNetCore.Components;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Sdk.UserManagement.Events;
using Sdk.Utils;
using ViciOne.Ui.Blazor.Components.Grid.Services;

namespace Blazor.Shared.UserManagement.ControlPanels.Role.Components;

[ControlPanelCategory<ControlPanelCategoryDescriptor>]
[ControlPanelGroup<SecondaryControlPanelGroupDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class RoleControlPanel : ControlPanelBase<RoleControlPanelState>,
    IEventConsumer<RoleCreatedEvent>, IEventConsumer<RoleUpdatedEvent>
{
    private readonly AutoDisposeList<IDisposable> _subscriptionHandle = [];
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _deletePermissionActionButtonEnabled;

    [Inject] private IUiMediator Mediator { get; set; } = default!;
    [Inject] private IAvailableClaimsService AvailableClaimsService { get; set; } = default!;
    [Inject] private IGridItemService GridItemService { get; set; } = default!;
    [Inject(Key = typeof(RolesControlPanelServiceKey))] private IGridItemSelection<PermissionGridItemId> PermissionsGridItemSelection { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        _subscriptionHandle.Add(Mediator.Register<RoleCreatedEvent>(this));
        _subscriptionHandle.Add(Mediator.Register<RoleUpdatedEvent>(this));

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

        _subscriptionHandle.Dispose();

        await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource.Dispose();

        await base.DisposeAsyncCore();
    }

    private async Task RoleNameTextBoxValueChanged(string value)
    {
        if (State.Role is null)
            return;

        State.Role.Name = value;
        await BeginEdit();
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

    private string GetDescriptionBannerTitle()
    {
        var culture = Localization.RoleControlPanel.Culture;
        return string.Format(culture, State.IsEditMode
            ? Localization.RoleControlPanel.DescriptionBannerTitleOnEdit
            : Localization.RoleControlPanel.DescriptionBannerTitleOnAdd);
    }

    private string GetDescriptionBannerContent()
    {
        var culture = Localization.RoleControlPanel.Culture;
        return string.Format(culture, State.IsEditMode
            ? Localization.RoleControlPanel.DescriptionBannerContentOnEdit
            : Localization.RoleControlPanel.DescriptionBannerContentOnAdd);
    }

    /// <remarks>
    /// Keeps the panel current when another session or instance changes roles. The switch from create to edit mode
    /// happens in <see cref="RoleControlPanelSaveHandler"/>, so a failed create keeps the panel in create mode.
    /// </remarks>
    private async Task RoleCreatedOrUpdated(ErrorInfo? errorInfo)
    {
        if (errorInfo is not null)
            return;

        if (State.Role is not null)
            await AvailableClaimsService.UpdateAvailableClaims(State);

        await InvokeAsync(StateHasChanged);
    }

    public async Task Consume(ClientContext<RoleCreatedEvent> context, CancellationToken cancellationToken)
        => await RoleCreatedOrUpdated(context.Message.ErrorInfo);

    public async Task Consume(ClientContext<RoleUpdatedEvent> context, CancellationToken cancellationToken)
        => await RoleCreatedOrUpdated(context.Message.ErrorInfo);
}
