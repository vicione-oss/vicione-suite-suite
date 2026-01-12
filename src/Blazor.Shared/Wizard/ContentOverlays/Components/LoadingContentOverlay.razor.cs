using Blazor.Shared.Popup.Factories;
using Blazor.Shared.Wizard.ContentOverlays.Models;
using Microsoft.AspNetCore.Components;
using ViciOne.Ui.Blazor.Components.LoadingSpinner.Models;

namespace Blazor.Shared.Wizard.ContentOverlays.Components;

public sealed partial class LoadingContentOverlay
{
    private TimedMessage? _timedMessage;
    private List<TimedMessage> _timedMessages = [];

    [CascadingParameter] private ContentOverlaySectionId SectionId { get; set; } = default!;

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
