using Sdk.Client.NotificationArea.Components;

namespace TestModule.Client.NotificationArea;

internal sealed class TestNotificationElementBadge : NotificationElementNumberBadgeBase
{
    protected override int? GetNumber() => 42;
}
