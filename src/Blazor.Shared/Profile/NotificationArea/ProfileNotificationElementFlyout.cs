using Sdk.Client.NotificationArea.Components;
using Sdk.Client.NotificationArea.Services;

namespace Blazor.Shared.Profile.NotificationArea;

internal sealed class ProfileNotificationElementFlyout : NotificationElementFlyout<NotificationElementState, ProfileNotificationElementFlyoutContent>
{
    protected override string? GetHeading() => null;
}
