using Sdk.MessageBanner.Contracts;
using ViciOne.SourceGenerators.NotifyChanged;
using ViciOne.Ui.MonochromeIcons.Core.Enums;

namespace Blazor.Shared.MessageBanner.NotificationArea;

[NotifyChanged]
public sealed partial class MessageBannerNotificationElementIconState
{
    private readonly Lock _concurrentLock = new();
    private int _updateLock;
    private int _changeCount;

    public partial MonochromeIconName Icon { get; set; }

    public partial MessageType MessageType { get; set; }

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
