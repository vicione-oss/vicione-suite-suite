using System.Globalization;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.DateAndTime.Services;
using Blazor.Shared.UserManagement.ControlPanels.User.Extensions;
using Blazor.Shared.UserManagement.ControlPanels.User.Services;
using Blazor.Shared.UserManagement.Services;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Requests;
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
using ViciOne.Ui.Blazor.Components.ComboBox;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.UserManagement.ControlPanels.User.Components;

[ControlPanelCategory<ControlPanelCategoryDescriptor>]
[ControlPanelGroup<SecondaryControlPanelGroupDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class UserControlPanel : ControlPanelBase<UserControlPanelState>,
    IEventConsumer<UserCreatedEvent>, IEventConsumer<UserUpdatedEvent>, IEventConsumer<RoleDeletedEvent>
{
    private readonly AutoDisposeList<IDisposable> _subscriptionHandle = [];
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _selfEdit;
    private List<ComboBoxItem<CultureInfo?, string>> _availableCultures = [];
    private List<ComboBoxItem<TimeZoneInfo?, string>> _availableTimeZones = [];

    [Inject] private IUiMediator Mediator { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private IEnumerable<IModuleFeature> Features { get; set; } = default!;
    [Inject] private IRoleService RoleService { get; set; } = default!;
    [Inject] private IModuleAuthorizationClaimParser ClaimsParser { get; set; } = default!;
    [Inject] private IClaimsProvider ClaimsProvider { get; set; } = default!;
    [Inject] private ITimeZoneDescriptorProvider TimeZoneDescriptorProvider { get; set; } = default!;

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

        await UpdateComboBoxItems();
    }

    private async Task UpdateComboBoxItems()
    {
        var requestResult = await Mediator.Request<GetCrossInstanceConfiguration, GetCrossInstanceConfigurationResponse>(
            new GetCrossInstanceConfiguration());

        var crossInstanceConfiguration = requestResult.CrossInstanceConfiguration;

        _availableCultures = [new() { Text = $"{CommonVocabulary.Default} - {new CultureInfo(crossInstanceConfiguration.CultureName).DisplayName}", Value = null }];
        _availableCultures.AddRange(CrossInstanceConfiguration.SupportedCultures.Select(c => new ComboBoxItem<CultureInfo?, string> { Text = c.DisplayName, Value = c }));

        _availableTimeZones = [new() { Text = CommonVocabulary.Default, Value = null }];

        try
        {
            var timeZoneDescriptors = await TimeZoneDescriptorProvider.GetAll(_cancellationTokenSource.Token);

            _availableTimeZones.AddRange(timeZoneDescriptors
                .OrderBy(Settings.DateAndTime.Constants.OrderByKeySelector)
                .Select(d => new ComboBoxItem<TimeZoneInfo?, string> { Text = d.DisplayName, Value = TimeZoneInfo.FindSystemTimeZoneById(d.TimeZoneId) }));
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here, we return gracefully
        }
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
        if (context.Message.ErrorInfo is not null || State.UserProfile is null)
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
            // The error is not surfaced to the user yet; the panel only re-renders.
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (userProfile.UserName != State.UserName)
            return;

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

    private string GetDescriptionBannerTitle(string? username)
    {
        var culture = Localization.UserControlPanel.Culture;
        return string.Format(culture, State.IsEditMode()
            ? ViciOne.Ui.Localization.Resources.UserActions.EditSomething
            : Localization.UserControlPanel.DescriptionBannerTitleOnAdd, username);
    }

    private string GetDescriptionBannerContent()
    {
        var culture = Localization.UserControlPanel.Culture;
        return string.Format(culture, State.IsEditMode()
            ? Localization.UserControlPanel.DescriptionBannerContentOnEdit
            : Localization.UserControlPanel.DescriptionBannerContentOnAdd);
    }
}
