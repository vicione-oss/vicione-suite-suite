using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.NotificationArea.Components;

namespace Blazor.Shared.Settings.NotificationArea;

public sealed partial class SettingsNotificationElementFlyoutContent : ComponentBase, INotificationElementFlyoutContent, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private string? _filter;

    [Inject] private IEnumerable<IUpdateControlPanelRegistryHandler> UpdateControlPanelRegistryHandlers { get; set; } = default!;
    [Inject] private ISettingsPopupRequest SettingsPopupRequest { get; set; } = default!;
    [Inject] private ILogger<SettingsNotificationElementFlyoutContent> Logger { get; set; } = default!;

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }

    private async Task OpenSettingsButtonClick()
    {
        try
        {
            var cancellationToken = _cancellationTokenSource.Token;

            await UpdateControlPanelRegistryHandlers.Execute(Logger, cancellationToken);

            if (!cancellationToken.IsCancellationRequested)
                SettingsPopupRequest.Send();
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here, return gracefully
        }
        catch (ObjectDisposedException)
        {
            // CancellationTokenSource already disposed, return gracefully
        }
    }
}
