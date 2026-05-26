using System.Globalization;
using Blazor.Shared.Services;
using Blazor.Shared.UserManagement.ControlPanels.User.Extensions;
using Blazor.Shared.UserManagement.ControlPanels.User.Services;
using Blazor.Shared.UserManagement.Services;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Sdk.UserManagement.Events;
using Sdk.Utils;

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
    private bool _selfEdit;

    [Inject] private IUiMediator Mediator { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private IEnumerable<IModuleFeature> Features { get; set; } = default!;
    [Inject] private IRoleService RoleService { get; set; } = default!;
    [Inject] private IModuleAuthorizationClaimParser ClaimsParser { get; set; } = default!;
    [Inject] private IClaimsProvider ClaimsProvider { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        _selfEdit = State.UserProfile is not null
                    && State.UserProfile.UserName.Value == authState.User.Identity!.Name
                    && State.IsEditMode();

        _subscriptionHandle.Add(Mediator.Register<UserCreatedEvent>(this));
        _subscriptionHandle.Add(Mediator.Register<UserUpdatedEvent>(this));
        _subscriptionHandle.Add(Mediator.Register<RoleDeletedEvent>(this));

        if (State.UserProfile?.PasswordExpirationDate is not null)
            State.PasswordExpirationDateString = State.UserProfile.PasswordExpirationDate.Value.ToString(User.Localization.Constants.UniversalDateFormat, CultureInfo.CurrentCulture);

        State.AvailableRoles = (await RoleService.GetAvailableRoles()).Select(r => r.Name);

        State.UpdateAvailableUserRoles();
        await State.UpdateAvailableClaims(ClaimsParser, ClaimsProvider);

        State.RoleToAdd = new(State.AvailableUserRoles?.FirstOrDefault() ?? string.Empty);

        State.Features = Features;

        State.UpdateGridItems(ClaimsParser);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _subscriptionHandle.Dispose();

        await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource.Dispose();

        await base.DisposeAsyncCore();
    }

    public async Task Consume(ClientContext<UserCreatedEvent> context, CancellationToken cancellationToken)
        => await UserCreatedOrUpdated(context.Message.UserProfile, context.Message.ErrorInfo);

    public async Task Consume(ClientContext<UserUpdatedEvent> context, CancellationToken cancellationToken)
        => await UserCreatedOrUpdated(context.Message.UserProfile, context.Message.ErrorInfo);

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

    private async Task UserCreatedOrUpdated(UserProfile userProfile, ErrorInfo? errorInfo)
    {
        if (errorInfo is not null)
        {
            // Optional: hook UI error handling here (dialog/toast/banner), then return.
            // Example (pseudo): State.SetError(userUpdatedEvent.ErrorInfo);
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (userProfile.UserName != State.UserName)
            return;

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
