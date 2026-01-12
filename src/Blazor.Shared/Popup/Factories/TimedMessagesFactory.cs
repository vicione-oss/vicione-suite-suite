using ViciOne.Ui.Blazor.Components.LoadingSpinner.Factories;
using ViciOne.Ui.Blazor.Components.LoadingSpinner.Models;

namespace Blazor.Shared.Popup.Factories;

internal static class TimedMessagesFactory
{
    public static IEnumerable<TimedMessage> CreateTimedMessages(TimedMessage? firstTimedMessage = null)
    {
        if (firstTimedMessage is not null)
            yield return firstTimedMessage;
        else
            yield return TimedMessageFactory.CreateGap(13);

        yield return new()
        {
            DisplayDuration = 8,
            Message = Localization.TimedMessagesFactory.TimedMessage1
        };

        yield return TimedMessageFactory.CreateGap(7);

        yield return new()
        {
            DisplayDuration = 8,
            Message = Localization.TimedMessagesFactory.TimedMessage2
        };

        yield return TimedMessageFactory.CreateGap(15);

        yield return new()
        {
            DisplayDuration = 8,
            Message = Localization.TimedMessagesFactory.TimedMessage3
        };

        yield return TimedMessageFactory.CreateGap(7);

        yield return new()
        {
            Message = Localization.TimedMessagesFactory.TimedMessage4
        };
    }
}
