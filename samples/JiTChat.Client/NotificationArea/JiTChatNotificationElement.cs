using JiTChat.Client.Localization;
using Sdk.Authorization;
using Sdk.Client.NotificationArea.Attributes;
using Sdk.Client.NotificationArea.Components;
using Sdk.Client.NotificationArea.Services;

namespace JiTChat.Client.NotificationArea;

[InitialNotificationElement<JiTChatClientModule>]
[ModuleAuthorize<JiTChatClientModule>]
public sealed class JiTChatNotificationElement : NotificationElement<NotificationElementState, JiTChatNotificationElementIcon>
{
    public JiTChatNotificationElement()
    {
        RegisterBadge<JiTChatNotificationElementBadge>();
        RegisterFlyout<JiTChatNotificationElementFlyout>();
    }

    protected override string GetTitle() => Common.ModuleTitle;
}
