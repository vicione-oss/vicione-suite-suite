using Sdk.Client.NotificationArea.Components;
using Sdk.Client.NotificationArea.Services;

namespace TestModule.Client.NotificationArea;

internal sealed class TestNotificationElementFlyout : NotificationElementFlyout<NotificationElementState, TestNotificationElementFlyoutContent>
{
    protected override string? GetHeading() => "Test-Flyout-Title";
}

