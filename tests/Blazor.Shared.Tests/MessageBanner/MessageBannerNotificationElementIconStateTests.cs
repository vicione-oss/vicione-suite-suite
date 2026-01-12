using Blazor.Shared.Enums;
using Blazor.Shared.MessageBanner.NotificationArea;
using AwesomeAssertions;
using Sdk.MessageBanner.Contracts;
using Xunit;

namespace Blazor.Shared.Tests.MessageBanner;

public sealed class MessageBannerNotificationElementIconStateTests
{
    [Theory]
    [InlineData(SvgIcon.InfoOutlined, SvgIcon.InfoOutlined, false)]
    [InlineData(SvgIcon.InfoOutlined, SvgIcon.CloseCircle, true)]
    public void Assert_changed_event_handling_when_icon_name_is_set(SvgIcon initialIcon,
        SvgIcon icon, bool shouldTriggerChangedEvent)
    {
        // Arrange
        var state = new MessageBannerNotificationElementIconState { Icon = initialIcon };
        var stateMonitor = state.Monitor();

        // Act
        state.Icon = icon;

        // Assert
        if (shouldTriggerChangedEvent)
            stateMonitor.Should().Raise(nameof(state.Changed));
        else
            stateMonitor.Should().NotRaise(nameof(state.Changed));
    }

    [Theory]
    [InlineData(MessageType.Information, MessageType.Information, false)]
    [InlineData(MessageType.Information, MessageType.Warning, true)]
    public void Assert_changed_event_handling_when_message_type_is_set(MessageType initialMessageType,
        MessageType messageType, bool shouldTriggerChangedEvent)
    {
        // Arrange
        var state = new MessageBannerNotificationElementIconState { MessageType = initialMessageType };
        var stateMonitor = state.Monitor();

        // Act
        state.MessageType = messageType;

        // Assert
        if (shouldTriggerChangedEvent)
            stateMonitor.Should().Raise(nameof(state.Changed));
        else
            stateMonitor.Should().NotRaise(nameof(state.Changed));
    }
}
