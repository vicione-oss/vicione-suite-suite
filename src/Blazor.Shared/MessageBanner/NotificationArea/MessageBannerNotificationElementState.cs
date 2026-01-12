using Sdk.Client.NotificationArea.Services;

namespace Blazor.Shared.MessageBanner.NotificationArea;

public sealed class MessageBannerNotificationElementState : NotificationElementState
{
    private string _title = string.Empty;

    public string Title
    {
        get => _title;
        set
        {
            if (value != _title)
            {
                _title = value;

                OnPropertyChanged();
            }
        }
    }
}
