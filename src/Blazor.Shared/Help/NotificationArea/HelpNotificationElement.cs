using Sdk.Client.NotificationArea.Components;
using Sdk.Client.NotificationArea.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Help.NotificationArea;

public sealed class HelpNotificationElement : NotificationElement<NotificationElementState, HelpNotificationElementIcon>
{
    public HelpNotificationElement()
        => RegisterFlyout<HelpNotificationElementFlyout>();

    protected override string GetTitle() => CommonVocabulary.Help;
}
