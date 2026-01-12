using Sdk.Client.NotificationArea.Services;

namespace Blazor.Shared.NotificationArea.Services;

internal sealed class SingleActiveNotificationElementPolicy : IActiveNotificationElementPolicy
{
    private readonly HashSet<INotificationElementState> _notificationElementStates = [];

    public void Include(INotificationElementState notificationElementState)
    {
        _notificationElementStates.Add(notificationElementState);

        notificationElementState.Changed -= OnNotificationElementStateChanged;
        notificationElementState.Changed += OnNotificationElementStateChanged;
    }

    public void Exclude(INotificationElementState notificationElementState)
    {
        notificationElementState.Changed -= OnNotificationElementStateChanged;

        _notificationElementStates.Remove(notificationElementState);
    }

    private void OnNotificationElementStateChanged(NotificationElementStateChangedEventArgs args)
    {
        if (args.PropertyNames.Contains(nameof(args.Sender.IsActive)))
        {
            if (args.Sender.IsActive)
            {
                var otherNotificationElementStates = _notificationElementStates.Except([args.Sender]).ToList();
                foreach (var otherNotificationElementState in otherNotificationElementStates)
                {
                    otherNotificationElementState.IsActive = false;
                }
            }
        }
    }

    public void NoneActive()
    {
        foreach (var notificationElementState in _notificationElementStates)
            notificationElementState.IsActive = false;
    }
}
