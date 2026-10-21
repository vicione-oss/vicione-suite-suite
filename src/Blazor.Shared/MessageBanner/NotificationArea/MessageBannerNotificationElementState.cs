using Sdk.Client.NotificationArea.Services;
using ViciOne.SourceGenerators.NotifyChanged;

namespace Blazor.Shared.MessageBanner.NotificationArea;

[NotifyChanged]
public sealed partial class MessageBannerNotificationElementState : NotificationElementState
{
    public partial string Title { get; set; } = string.Empty;
}
