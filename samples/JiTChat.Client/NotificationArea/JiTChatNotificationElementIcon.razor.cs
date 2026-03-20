using Microsoft.AspNetCore.Components;
using Sdk.Client.NotificationArea.Services;

namespace JiTChat.Client.NotificationArea;

public sealed partial class JiTChatNotificationElementIcon : ComponentBase, IDisposable
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
