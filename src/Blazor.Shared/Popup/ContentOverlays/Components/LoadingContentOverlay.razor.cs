using Blazor.Shared.Popup.ContentOverlays.Models;
using Blazor.Shared.Popup.Factories;
using Microsoft.AspNetCore.Components;
using ViciOne.Ui.Blazor.Components.LoadingSpinner.Models;

namespace Blazor.Shared.Popup.ContentOverlays.Components;

public sealed partial class LoadingContentOverlay
{
    private TimedMessage? _timedMessage;
    private List<TimedMessage> _timedMessages = [];

    [Parameter] public ContentOverlaySectionId? SectionId { get; set; }

    [Parameter] public TimedMessage? TimedMessage { get; set; }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (TimedMessage != _timedMessage || _timedMessages.Count == 0)
        {
            _timedMessages = [.. TimedMessagesFactory.CreateTimedMessages(TimedMessage)];

            _timedMessage = TimedMessage;
        }
    }
}
