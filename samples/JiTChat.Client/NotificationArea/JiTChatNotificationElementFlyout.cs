using System.Diagnostics.CodeAnalysis;
using JiTChat.Client.Localization;
using Sdk.Client.NotificationArea.Components;
using Sdk.Client.NotificationArea.Services;

namespace JiTChat.Client.NotificationArea;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by DI container")]
internal sealed class JiTChatNotificationElementFlyout : NotificationElementFlyout<NotificationElementState, JiTChatNotificationElementFlyoutContent>
{
    protected override string? GetHeading() => Common.ModuleTitle;
}
