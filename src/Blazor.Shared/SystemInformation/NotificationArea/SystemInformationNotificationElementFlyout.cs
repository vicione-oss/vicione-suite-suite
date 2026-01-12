using Sdk.Client.NotificationArea.Components;
using Sdk.Client.NotificationArea.Services;

namespace Blazor.Shared.SystemInformation.NotificationArea;

internal sealed class SystemInformationNotificationElementFlyout : NotificationElementFlyout<NotificationElementState, SystemInformationNotificationElementFlyoutContent>
{
    protected override string? GetHeading() => SystemInformation.Localization.Common.FeatureName;
}
