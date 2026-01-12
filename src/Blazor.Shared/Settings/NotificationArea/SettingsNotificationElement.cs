using Blazor.Shared.Models;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.NotificationArea.Attributes;
using Sdk.Client.NotificationArea.Components;
using Sdk.Client.NotificationArea.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Settings.NotificationArea;

[InitialNotificationElement<SharedClientModule>(Position = int.MinValue, Visible = false)]
public sealed class SettingsNotificationElement : NotificationElement<NotificationElementState, SettingsNotificationElementIcon>
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    [Inject] private ISettingsPopupRequest SettingsPopupRequest { get; set; } = default!;
    [Inject] private ISettingsPopupState SettingsPopupState { get; set; } = default!;
    [Inject] private IEnumerable<IUpdateControlPanelRegistryHandler> UpdateControlPanelRegistryHandlers { get; set; } = default!;
    [Inject] private ILogger<SettingsNotificationElement> Logger { get; set; } = default!;

    public SettingsNotificationElement()
    {
        //RegisterFlyout<SettingsNotificationElementFlyout>();
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        try
        {
            var cancellationToken = _cancellationTokenSource.Token;

            await UpdateControlPanelRegistryHandlers.Execute(Logger, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here, return gracefully

            return;
        }
        catch (ObjectDisposedException)
        {
            // CancellationTokenSource already disposed, return gracefully

            return;
        }

        SettingsPopupState.Changed += SettingsPopupStateChanged;

        State.Visible = true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SettingsPopupState.Changed -= SettingsPopupStateChanged;

            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override string GetTitle() => CommonVocabulary.SettingPlural;

    protected override void OnClick()
        => SettingsPopupRequest.Send();

    private void SettingsPopupStateChanged(PropertiesChangedEventArgs args)
    {
        if (args.PropertyNames.Contains(nameof(ISettingsPopupState.Visible)))
            State.IsActive = SettingsPopupState.Visible;
    }
}
