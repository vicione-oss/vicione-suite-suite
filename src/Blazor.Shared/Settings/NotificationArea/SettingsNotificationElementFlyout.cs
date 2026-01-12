using Sdk.Client.NotificationArea.Components;
using Sdk.Client.NotificationArea.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Settings.NotificationArea;

internal sealed class SettingsNotificationElementFlyout : NotificationElementFlyout<NotificationElementState, SettingsNotificationElementFlyoutContent>
{
    protected override string? GetHeading() => CommonVocabulary.SettingPlural;
}
