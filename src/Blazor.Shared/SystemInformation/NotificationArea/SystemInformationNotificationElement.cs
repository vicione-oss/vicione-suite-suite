using Sdk.Client.NotificationArea.Attributes;
using Sdk.Client.NotificationArea.Components;
using Sdk.Client.NotificationArea.Services;

namespace Blazor.Shared.SystemInformation.NotificationArea;

[InitialNotificationElement<SharedClientModule>(Position = 3)]
public sealed class SystemInformationNotificationElement : NotificationElement<NotificationElementState, SystemInformationNotificationElementIcon>
{
    public SystemInformationNotificationElement()
    {
        RegisterFlyout<SystemInformationNotificationElementFlyout>();
    }

    protected override string GetTitle() => SystemInformation.Localization.Common.FeatureName;
}
