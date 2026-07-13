using Blazor.Shared.MessageBanner.NotificationArea;
using AwesomeAssertions;
using Xunit;

namespace Blazor.Shared.Tests.MessageBanner;

public sealed class MessageBannerNotificationElementStateTests
{
    [Theory]
    [InlineData("", "", false)]
    [InlineData("", "Random title", true)]
    [InlineData("Random title", "", true)]
    [InlineData("Random title", "Random title", false)]
    public void Should_handle_changed_event_when_title_is_set(string initialTitle, string title, bool shouldTriggerChangedEvent)
    {
        // Arrange
        var changedTriggered = false;

        var state = new MessageBannerNotificationElementState { Title = initialTitle };
        state.Changed += args => changedTriggered = args.PropertyNames.Contains(nameof(MessageBannerNotificationElementState.Title));

        // Act
        state.Title = title;

        // Assert
        changedTriggered.Should().Be(shouldTriggerChangedEvent);
    }
}
