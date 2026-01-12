using Blazor.Shared.Enums;
using Sdk.MessageBanner.Contracts;

namespace Blazor.Shared.MessageBanner.NotificationArea;

public sealed class MessageBannerNotificationElementIconState
{
    private readonly Lock _concurrentLock = new();
    private int _updateLock;
    private int _changeCount;

    private SvgIcon _icon;
    private MessageType _messageType;

    public SvgIcon Icon
    {
        get => _icon;
        set
        {
            if (value != _icon)
            {
                _icon = value;

                OnPropertyChanged();
            }
        }
    }

    public MessageType MessageType
    {
        get => _messageType;
        set
        {
            if (value != _messageType)
            {
                _messageType = value;

                OnPropertyChanged();
            }
        }
    }

    public event Action? Changed;

    private void OnPropertyChanged()
    {
        if (_updateLock == 0)
            Changed?.Invoke();
        else
            _changeCount++;
    }

    public void BeginUpdate()
    {
        lock (_concurrentLock)
        {
            _updateLock++;
        }
    }

    public void EndUpdate()
    {
        lock (_concurrentLock)
        {
            _updateLock--;

            if (_updateLock <= 0)
            {
                _updateLock = 0;

                if (_changeCount == 0)
                    return;

                Changed?.Invoke();

                _changeCount = 0;
            }
        }
    }
}
