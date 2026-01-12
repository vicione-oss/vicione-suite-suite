using Sdk.Client.NotificationArea.Components;
using Sdk.Client.NotificationArea.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Help.NotificationArea;

internal sealed class HelpNotificationElementFlyout : NotificationElementFlyout<NotificationElementState, HelpNotificationElementFlyoutContent>
{
    protected override string? GetHeading() => CommonVocabulary.Help;
}
