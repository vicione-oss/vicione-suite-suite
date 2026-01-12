using Sdk.Client.NotificationArea.Attributes;
using Sdk.Client.NotificationArea.Components;
using Sdk.Client.NotificationArea.Services;
using ViciOne.Ui.Localization.Resources;


namespace Blazor.Shared.Profile.NotificationArea;

[InitialNotificationElement<SharedClientModule>]
public sealed class ProfileNotificationElement : NotificationElement<NotificationElementState, ProfileNotificationElementIcon>
{
    public ProfileNotificationElement()
    {
        RegisterFlyout<ProfileNotificationElementFlyout>();
    }

    protected override string GetTitle() => CommonVocabulary.Profile;
}
