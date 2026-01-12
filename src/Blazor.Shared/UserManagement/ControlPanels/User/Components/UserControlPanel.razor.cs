using System.Globalization;
using Blazor.Shared.Services;
using Blazor.Shared.UserManagement.ControlPanels.User.Models;
using Blazor.Shared.UserManagement.ControlPanels.User.Services;
using Blazor.Shared.UserManagement.Services;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using Core.Shared.UserManagement.Extensions;
using Microsoft.AspNetCore.Components;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.Infrastructure;
using Sdk.Utils;
using ViciOne.Ui.Blazor.Components.Grid.Services;

namespace Blazor.Shared.UserManagement.ControlPanels.User.Components;

[ControlPanelCategory<ControlPanelCategoryDescriptor>]
[ControlPanelGroup<SecondaryControlPanelGroupDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class UserControlPanel : ControlPanelBase<UserControlPanelState>,
    IEventConsumer<UserCreatedEvent>, IEventConsumer<UserUpdatedEvent>
{
    private bool _twoFactorAuthenticationEnabled;
    private readonly AutoDisposeList<IDisposable> _subscriptionHandle = [];
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _deleteRoleActionButtonEnabled;
    private bool _deletePermissionActionButtonEnabled;

    private IQueryable<Role>? _assignedUserRoles => State.UserProfile?.Roles
        .OrderBy(k => k.Value)
        .AsQueryable();

    [Inject] private IUiMediator Mediator { get; set; } = default!;
    [Inject] private IEnumerable<IModuleFeature> Features { get; set; } = default!;
    [Inject] private IRolesProvider RolesProvider { get; set; } = default!;
    [Inject(Key = typeof(UserControlPanelServiceKey))] private IGridItemSelection<Role> RolesGridItemSelection { get; set; } = default!;
    [Inject(Key = typeof(UserControlPanelServiceKey))] private IGridItemSelection<PermissionGridItemId> PermissionsGridItemSelection { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        PermissionsGridItemSelection.Clear();
        RolesGridItemSelection.Clear();

        RolesGridItemSelection.Changed += RolesGridItemSelectionChanged;
        PermissionsGridItemSelection.Changed += PermissionsGridItemSelectionChanged;

        _subscriptionHandle.Add(Mediator.Register<UserCreatedEvent>(this));
        _subscriptionHandle.Add(Mediator.Register<UserUpdatedEvent>(this));

        if (State.UserProfile?.PasswordExpirationDate is not null)
            State.PasswordExpirationDateString = State.UserProfile.PasswordExpirationDate.Value.ToString(User.Localization.Constants.UniversalDateFormat, CultureInfo.CurrentCulture);

        State.AvailableRoles = await RolesProvider.GetAvailableRoles();

        State.UpdateAvailableUserRoles();
        await State.UpdateAvailableClaims();

        State.RoleToAdd = State.AvailableUserRoles?.FirstOrDefault();

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

    private async void RolesGridItemSelectionChanged(GridItemSelectionChangedEventArgs<Role> obj)
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

        State.UserProfile?.Roles.Add(State.RoleToAdd.Value);

        State.UpdateAvailableUserRoles();

        State.RoleToAdd = State.AvailableUserRoles?.FirstOrDefault();

        await BeginEdit();
    }

    private async Task RemoveSelectedRoles()
    {
        if (RolesGridItemSelection.Count == 0)
            return;

        foreach (var role in RolesGridItemSelection)
        {
            State.UserProfile?.Roles.Remove(role);
        }

        State.UpdateAvailableUserRoles();

        if (State.AvailableRoles is not null && State.AvailableRoles.All(k => k != State.RoleToAdd))
        {
            State.RoleToAdd = State.AvailableUserRoles?.FirstOrDefault();
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
                .ToUserProfileClaim();
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
