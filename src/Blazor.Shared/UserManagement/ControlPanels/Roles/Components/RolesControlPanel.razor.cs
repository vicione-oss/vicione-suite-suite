using Blazor.Shared.Services;
using Blazor.Shared.UserManagement.ControlPanels.Role.Components;
using Blazor.Shared.UserManagement.ControlPanels.Role.Services;
using Blazor.Shared.UserManagement.ControlPanels.Roles.Models;
using Blazor.Shared.UserManagement.ControlPanels.Roles.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.UserManagement.Events;
using Sdk.Utils;
using ViciOne.Ui.Blazor.Components.Grid.Services;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.UserManagement.ControlPanels.Roles.Components;

[ControlPanelCategory<ControlPanelCategoryDescriptor>]
[ControlPanelGroup<SecondaryControlPanelGroupDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class RolesControlPanel : ControlPanelBase<RolesControlPanelState>,
    IEventConsumer<RoleCreatedEvent>, IEventConsumer<RoleDeletedEvent>
{
    private readonly string _descriptionBannerIconCssClass = MonochromeIconName.UserLight.GetCssClasses().ToSpaceSeparated();
    private readonly AutoDisposeList<IDisposable> _subscriptions = [];

    private IQueryable<Sdk.UserManagement.Contracts.Role>? _rolesQueryable;
    private string? _filterText;
    private bool _isSingleRoleSelected;
    private bool _deleteGridActionButtonEnabled;

    [Inject] private IUiMediator Mediator { get; set; } = default!;
    [Inject] private IControlPanelRequest ControlPanelRequest { get; set; } = default!;
    [Inject(Key = typeof(RolesControlPanelServiceKey))] private IGridItemSelection<string> RoleGridItemSelection { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        _subscriptions.Add(Mediator.Register<RoleCreatedEvent>(this));
        _subscriptions.Add(Mediator.Register<RoleDeletedEvent>(this));

        State.Changed += StateChanged;
        RoleGridItemSelection.Changed += RoleGridItemSelectionChanged;

        UpdateStatesDependingOnRoleGridItemSelection();

        UpdateRolesQueryable();
    }

    protected override ValueTask DisposeAsyncCore()
    {
        State.Changed -= StateChanged;
        RoleGridItemSelection.Changed -= RoleGridItemSelectionChanged;

        _subscriptions.Dispose();

        return base.DisposeAsyncCore();
    }

    private void UpdateRolesQueryable()
    {
        _rolesQueryable = State.Roles.AsQueryable();

        if (!string.IsNullOrWhiteSpace(_filterText))
        {
            _rolesQueryable = _rolesQueryable.Where(r => (r.Name != null && r.Name.Contains(_filterText, StringComparison.CurrentCultureIgnoreCase)) ||
                (r.Description != null && r.Description.Contains(_filterText, StringComparison.CurrentCultureIgnoreCase)));
        }

        _rolesQueryable = _rolesQueryable.OrderBy(u => u.Name);
    }

    private bool UpdateStatesDependingOnRoleGridItemSelection()
    {
        var somethingChanged = false;

        var isSingleUserSelected = RoleGridItemSelection.Count == 1;

        if (isSingleUserSelected != _isSingleRoleSelected)
        {
            _isSingleRoleSelected = isSingleUserSelected;
            somethingChanged = true;
        }

        var deleteGridActionButtonEnabled = false;
        if (RoleGridItemSelection.Count > 0)
        {
            deleteGridActionButtonEnabled = RoleGridItemSelection.Any(name => !State.Roles.First(r => r.Name == name).Managed);
        }

        if (deleteGridActionButtonEnabled != _deleteGridActionButtonEnabled)
        {
            _deleteGridActionButtonEnabled = deleteGridActionButtonEnabled;
            somethingChanged = true;
        }

        return somethingChanged;
    }

    private async void RoleGridItemSelectionChanged(GridItemSelectionChangedEventArgs<string> args)
    {
        if (UpdateStatesDependingOnRoleGridItemSelection())
            await InvokeAsync(StateHasChanged);
    }

    private async void StateChanged(ControlPanelStateChangedEventArgs obj)
    {
        var reload = false;

        if (obj.PropertyNames.Contains(nameof(State.Roles)) && State.ResetSelectedRoles)
        {
            RoleGridItemSelection.BeginUpdate();

            try
            {
                foreach (var user in State.DeletingRoles)
                    RoleGridItemSelection.Add(user.Name);
            }
            finally
            {
                RoleGridItemSelection.EndUpdate();
            }

            State.ResetSelectedRoles = false;

            reload = true;
        }

        if (obj.PropertyNames.Contains(nameof(State.Roles)))
        {
            UpdateRolesQueryable();
            UpdateStatesDependingOnRoleGridItemSelection();

            reload = true;
        }

        if (reload)
            await InvokeAsync(StateHasChanged);
    }

    private async Task AddRole()
        => await ControlPanelRequest.Send<RoleControlPanel, RoleControlPanelState>(
            s => s.RoleName = null);

    private async Task EditRole(Sdk.UserManagement.Contracts.Role role)
        => await ControlPanelRequest.Send<RoleControlPanel, RoleControlPanelState>(s => s.RoleName = role.Name);

    private async Task EditSelectedRole()
        => await ControlPanelRequest.Send<RoleControlPanel, RoleControlPanelState>(
            s => s.RoleName = RoleGridItemSelection.FirstOrDefault());

    private void DeleteSelectedRoles()
    {
        RoleGridItemSelection.BeginUpdate();

        try
        {
            foreach (var selectedRoleName in RoleGridItemSelection)
            {
                var roles = State.Roles
                    .Where(r => r.Name == selectedRoleName && !r.Managed)
                    .ToArray();

                if (!roles.Any())
                    continue;

                State.DeletingRoles.AddRange(roles);
                RoleGridItemSelection.Remove(roles.First().Name);
                State.Roles.Remove(roles.First());
            }
        }
        finally
        {
            RoleGridItemSelection.EndUpdate();
        }

        UpdateRolesQueryable();
        BeginEdit();
    }

    private void ApplyFilter()
        => UpdateRolesQueryable();

    public async Task Consume(ClientContext<RoleCreatedEvent> context, CancellationToken cancellationToken)
    {
        if (context.Message.ErrorInfo is not null)
            return;

        State.Roles.Add(context.Message.Role);

        UpdateStatesDependingOnRoleGridItemSelection();
        UpdateRolesQueryable();

        await InvokeAsync(StateHasChanged);
    }

    public async Task Consume(ClientContext<RoleDeletedEvent> context, CancellationToken cancellationToken)
    {
        if (context.Message.ErrorInfo is not null)
            return;

        var deleted = State.Roles.RemoveAll(k => k.Name == context.Message.Role.Name);
        if (deleted == 0)
            return;

        UpdateStatesDependingOnRoleGridItemSelection();
        UpdateRolesQueryable();

        await InvokeAsync(StateHasChanged);
    }
}
