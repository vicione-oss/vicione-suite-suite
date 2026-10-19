using Blazor.Shared.Services;
using Blazor.Shared.UserManagement.ControlPanels.Role.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Sdk.UserManagement.Events;
using Sdk.Utils;

namespace Blazor.Shared.UserManagement.ControlPanels.Role.Components;

[ControlPanelCategory<ControlPanelCategoryDescriptor>]
[ControlPanelGroup<SecondaryControlPanelGroupDescriptor>]
[ControlPanelParent<RoleControlPanelDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class RoleControlPanel : ControlPanelBase<RoleControlPanelState>,
    IEventConsumer<RoleCreatedEvent>, IEventConsumer<RoleUpdatedEvent>
{
    private readonly AutoDisposeList<IDisposable> _subscriptionHandle = [];
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    [Inject] private IUiMediator Mediator { get; set; } = default!;
    [Inject] private IAvailableClaimsService AvailableClaimsService { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        _subscriptionHandle.Add(Mediator.Register<RoleCreatedEvent>(this));
        _subscriptionHandle.Add(Mediator.Register<RoleUpdatedEvent>(this));
    }

    protected override async ValueTask DisposeAsyncCore()
    {
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
