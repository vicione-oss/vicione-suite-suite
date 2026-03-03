using System.Globalization;
using System.Security.Claims;
using Blazor.Shared.Services;
using Blazor.Shared.UserManagement.ControlPanels.User.Models;
using Blazor.Shared.UserManagement.ControlPanels.User.Services;
using Blazor.Shared.UserManagement.Models;
using Blazor.Shared.UserManagement.Services;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using Core.Shared.UserManagement.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.Infrastructure;
using Sdk.UserManagement.Events;
using Sdk.Utils;
using ViciOne.Ui.Blazor.Components.Grid.Services;

namespace Blazor.Shared.UserManagement.ControlPanels.User.Components;

[ControlPanelCategory<ControlPanelCategoryDescriptor>]
[ControlPanelGroup<SecondaryControlPanelGroupDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class UserControlPanel : ControlPanelBase<UserControlPanelState>,
    IEventConsumer<UserCreatedEvent>, IEventConsumer<UserUpdatedEvent>, IEventConsumer<RoleDeletedEvent>
{
    private bool _twoFactorAuthenticationEnabled;
    private readonly AutoDisposeList<IDisposable> _subscriptionHandle = [];
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _deleteRoleActionButtonEnabled;
    private bool _deletePermissionActionButtonEnabled;
    private bool _selfEdit;

    private IQueryable<string>? _assignedUserRoles => State.UserProfile?.Roles
        .Order()
        .AsQueryable();

    [Inject] private IUiMediator Mediator { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private IEnumerable<IModuleFeature> Features { get; set; } = default!;
    [Inject] private IRoleService RoleService { get; set; } = default!;
    [Inject(Key = typeof(UserControlPanelServiceKey))] private IGridItemSelection<string> RolesGridItemSelection { get; set; } = default!;
    [Inject(Key = typeof(UserControlPanelServiceKey))] private IGridItemSelection<PermissionGridItemId> PermissionsGridItemSelection { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        _selfEdit = State.UserProfile is not null
                    && State.UserProfile.UserName.Value == authState.User.Identity!.Name
                    && State.IsEditMode();

        PermissionsGridItemSelection.Clear();
        RolesGridItemSelection.Clear();

        RolesGridItemSelection.Changed += RolesGridItemSelectionChanged;
        PermissionsGridItemSelection.Changed += PermissionsGridItemSelectionChanged;

        _subscriptionHandle.Add(Mediator.Register<UserCreatedEvent>(this));
        _subscriptionHandle.Add(Mediator.Register<UserUpdatedEvent>(this));
        _subscriptionHandle.Add(Mediator.Register<RoleDeletedEvent>(this));

        if (State.UserProfile?.PasswordExpirationDate is not null)
            State.PasswordExpirationDateString = State.UserProfile.PasswordExpirationDate.Value.ToString(User.Localization.Constants.UniversalDateFormat, CultureInfo.CurrentCulture);

        State.AvailableRoles = (await RoleService.GetAvailableRoles()).Select(r => r.Name ?? string.Empty);

        State.UpdateAvailableUserRoles();
        await State.UpdateAvailableClaims();

        State.RoleToAdd = new(State.AvailableUserRoles?.FirstOrDefault() ?? string.Empty);

        State.Features = Features;

        State.UpdateGridItems();
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

    private async void RolesGridItemSelectionChanged(GridItemSelectionChangedEventArgs<string> obj)
    {
        var state = RolesGridItemSelection.Count > 0;
        if (state == _deleteRoleActionButtonEnabled)
            return;

        _deleteRoleActionButtonEnabled = state;
        await InvokeAsync(StateHasChanged);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        RolesGridItemSelection.Changed -= RolesGridItemSelectionChanged;
        PermissionsGridItemSelection.Changed -= PermissionsGridItemSelectionChanged;

        _subscriptionHandle.Dispose();

        await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource.Dispose();

        await base.DisposeAsyncCore();
    }

    public async Task Consume(ClientContext<UserCreatedEvent> context, CancellationToken cancellationToken)
        => await UserCreatedOrUpdated(context.Message.UserProfile);

    public async Task Consume(ClientContext<UserUpdatedEvent> context, CancellationToken cancellationToken)
        => await UserCreatedOrUpdated(context.Message.UserProfile);

    public async Task Consume(ClientContext<RoleDeletedEvent> context, CancellationToken cancellationToken)
    {
        if (State.UserProfile is null)
            return;

        var roleToRemove = State.UserProfile.Roles.FirstOrDefault(r => r == context.Message.Role.Name);

        if (roleToRemove is null)
            return;

        State.AvailableRoles = (await RoleService.GetAvailableRoles(cancellationToken)).Select(r => r.Name ?? string.Empty);
        State.UserProfile.Roles.Remove(roleToRemove);
        State.UpdateAvailableUserRoles();
        State.RoleToAdd = new(State.AvailableUserRoles?.FirstOrDefault() ?? string.Empty);

        await InvokeAsync(StateHasChanged);
    }

    private async Task UserCreatedOrUpdated(UserProfile userProfile)
    {
        State.UserName = userProfile.UserName;

        if (State.UserProfile is not null)
        {
            State.UserProfile.Roles = userProfile.Roles;
            State.UpdateAvailableUserRoles();
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task UserNameTextBoxValueChanged(string value)
    {
        if (State.UserProfile is null)
            return;

        State.UserProfile.UserName = new UserName(value);
        await BeginEdit();
    }

    private async Task AddToRole()
    {
        if (State.RoleToAdd is null)
            return;

        State.UserProfile?.Roles.Add(State.RoleToAdd ?? string.Empty);

        State.UpdateAvailableUserRoles();

        State.RoleToAdd = new(State.AvailableUserRoles?.FirstOrDefault() ?? string.Empty);

        await BeginEdit();
    }

    private async Task RemoveSelectedRoles()
    {
        if (RolesGridItemSelection.Count == 0)
            return;

        foreach (var role in RolesGridItemSelection)
            State.UserProfile?.Roles.Remove(role ?? string.Empty);

        State.UpdateAvailableUserRoles();

        if (State.AvailableRoles is not null && State.AvailableRoles.All(k => k != State.RoleToAdd))
        {
            State.RoleToAdd = new(State.AvailableUserRoles?.FirstOrDefault() ?? string.Empty);
        }

        RolesGridItemSelection.Clear();

        await BeginEdit();
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

    private string GetDescriptionBannerTitle()
    {
        var culture = Localization.UserControlPanel.Culture;
        return string.Format(culture, State.IsEditMode()
            ? Localization.UserControlPanel.DescriptionBannerTitleOnEdit
            : Localization.UserControlPanel.DescriptionBannerTitleOnAdd);
    }

    private string GetDescriptionBannerContent()
    {
        var culture = Localization.UserControlPanel.Culture;
        return string.Format(culture, State.IsEditMode()
            ? Localization.UserControlPanel.DescriptionBannerContentOnEdit
            : Localization.UserControlPanel.DescriptionBannerContentOnAdd);
    }
}
