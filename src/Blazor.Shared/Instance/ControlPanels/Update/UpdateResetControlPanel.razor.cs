using Blazor.Shared.Instance.ControlPanels.Update.Services;
using Blazor.Shared.Services;
using Core.Shared;
using Core.Shared.HostManagement.Events;
using Microsoft.AspNetCore.Components;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using Sdk.MessageBanner.Contracts;
using Sdk.Utils;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Instance.ControlPanels.Update;

[ControlPanelCategory<ControlPanelUpdateAndRestoreCategoryDescriptor>]
[ControlPanelChild<UpdateControlPanelDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class UpdateResetControlPanel : ControlPanelBase<UpdateResetControlPanelState>,
    IEventConsumer<ControlSystemCompleted>
{
    private readonly string _closeIconCssClass = MonochromeIconName.CloseMedium.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();
    private readonly string _refreshButtonIconCssClass = MonochromeIconName.Refresh.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();
    private readonly string _refreshIconCssClass = MonochromeIconName.Refresh.GetCssClasses().ToSpaceSeparated();

    private readonly AutoDisposeList<IDisposable> _subscriptionHandle = [];

    private bool _confirmation;
    private bool _showResetConfirmSection;

    [Inject]
    public IUiMediator Mediator { get; set; } = default!;

    [Inject]
    public IMessageBannerService BannerService { get; set; } = default!;

    [Inject]
    private UpdateResetService ResetService { get; set; } = default!;

    [Inject]
    private INavigationService NavigationManager { get; set; } = default!;

    private bool ShowResetConfirmSection
        => _showResetConfirmSection || ResetService.IsResetInProgress;

    protected override void OnInitialized()
    {
        base.OnInitialized();

        _subscriptionHandle.Add(Mediator.Register<ControlSystemCompleted>(this));
        ResetService.Changed += OnResetServiceChanged;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        ResetService.Changed -= OnResetServiceChanged;
        _subscriptionHandle.Dispose();

        await base.DisposeAsyncCore();
    }

    private void OnResetServiceChanged()
        => _ = InvokeAsync(StateHasChanged);

    private void OnResetButtonClick()
    {
        ResetService.ClearError();
        _showResetConfirmSection = true;
    }

    private Task OnResetConfirmedButtonClick()
        => ResetService.StartReset();

    private void OnNoButtonClick()
    {
        _confirmation = false;
        _showResetConfirmSection = false;
    }

    public async Task Consume(ClientContext<ControlSystemCompleted> context, CancellationToken cancellationToken)
    {
        // A reset of another user is not correlated, but logs out every user seeing this control panel.
        if (context.Message.ErrorInfo is not null || ResetService.IsOwnReset(context.Message.CorrelationId))
            return;

        BannerService.ShowMessageBanner(MessageType.Information, Localization.UpdateControlPanel.ResetInProgressMessage);

        // No delay, because the restart follows immediately and leaves no time to read the banner anyway.
        await NavigationManager.Logout();
    }
}
