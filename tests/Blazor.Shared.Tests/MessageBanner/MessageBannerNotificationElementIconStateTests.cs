using AwesomeAssertions;
using Blazor.Shared.MessageBanner.NotificationArea;
using Sdk.MessageBanner.Contracts;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Factories;
using Xunit;

namespace Blazor.Shared.Tests.MessageBanner;

public sealed class MessageBannerNotificationElementIconStateTests
{
    [Theory]
    [InlineData(nameof(MonochromeIconName.InfoLight), nameof(MonochromeIconName.InfoLight), false)]
    [InlineData(nameof(MonochromeIconName.InfoLight), nameof(MonochromeIconName.CloseCircleSolid), true)]
    public void Assert_changed_event_handling_when_icon_name_is_set(string initialIcon,
        string icon, bool shouldTriggerChangedEvent)
    {
        // Arrange
        var state = new MessageBannerNotificationElementIconState { Icon = TypeSafeEnumFactory<MonochromeIconName>.Create(initialIcon) };
        var stateMonitor = state.Monitor();

        // Act
        state.Icon = TypeSafeEnumFactory<MonochromeIconName>.Create(icon);

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
