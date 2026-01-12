using Blazor.Shared.MessageBanner.Models;

namespace Blazor.Shared.MessageBanner.Components;

public sealed class MessageBannerDialogState
{
    private readonly Lock _concurrentLock = new();
    private bool _visible;
    private IMessage _message = new Message();
    private int _updateLock;
    private int _changeCount;

    public IMessage Message
    {
        get => _message;
        set
        {
            if (value != _message)
            {
                _message = value;

                OnPropertyChanged();
            }
        }
    }

    public bool Visible
    {
        get => _visible;
        set
        {
            if (value != _visible)
            {
                _visible = value;

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
