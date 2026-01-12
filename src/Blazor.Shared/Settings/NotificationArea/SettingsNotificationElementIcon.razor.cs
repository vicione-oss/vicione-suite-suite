using Microsoft.AspNetCore.Components;
using Sdk.Client.NotificationArea.Services;

namespace Blazor.Shared.Settings.NotificationArea;

public sealed partial class SettingsNotificationElementIcon : ComponentBase, IDisposable
{
    [CascadingParameter]
    public required NotificationElementState NotificationElementState { get; set; }

    protected override void OnInitialized()
        => NotificationElementState.Changed += OnNotificationElementStateChanged;

    public void Dispose()
        => NotificationElementState.Changed -= OnNotificationElementStateChanged;

    private void OnNotificationElementStateChanged(NotificationElementStateChangedEventArgs args)
    {
        if (args.PropertyNames.Contains(nameof(NotificationElementState.IsActive)))
            InvokeAsync(StateHasChanged);
    }
}
