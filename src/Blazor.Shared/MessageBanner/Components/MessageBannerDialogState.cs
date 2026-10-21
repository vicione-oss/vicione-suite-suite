using Blazor.Shared.MessageBanner.Models;
using ViciOne.SourceGenerators.NotifyChanged;

namespace Blazor.Shared.MessageBanner.Components;

[NotifyChanged]
public sealed partial class MessageBannerDialogState
{
    private readonly Lock _concurrentLock = new();
    private int _updateLock;
    private int _changeCount;

    public partial IMessage Message { get; set; } = new Message();

    public partial bool Visible { get; set; }

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
