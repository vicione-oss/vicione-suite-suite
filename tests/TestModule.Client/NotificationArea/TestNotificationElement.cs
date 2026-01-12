using Sdk.Client.NotificationArea.Attributes;
using Sdk.Client.NotificationArea.Components;
using Sdk.Client.NotificationArea.Services;

namespace TestModule.Client.NotificationArea;

[InitialNotificationElement<TestClientModule>(Position = 1)]
public sealed class TestNotificationElement : NotificationElement<NotificationElementState, TestNotificationIcon>
{
    public TestNotificationElement()
    {
        RegisterBadge<TestNotificationElementBadge>();
        RegisterFlyout<TestNotificationElementFlyout>();
    }

    protected override string GetTitle() => "Test-Title";
}
