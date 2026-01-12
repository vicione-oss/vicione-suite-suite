using Blazor.Shared.MessageBanner.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Client.NotificationArea.Attributes;
using Sdk.Client.NotificationArea.Components;
using Sdk.Client.NotificationArea.Services;

namespace Blazor.Shared.MessageBanner.NotificationArea;

[InitialNotificationElement<SharedClientModule>(Visible = false)]
public sealed class MessageBannerNotificationElement : NotificationElement<MessageBannerNotificationElementState, MessageBannerNotificationElementIcon>
{
    [Inject]
    public required IMessageBannerMediator MessageBannerMediator { get; set; }

    protected override string GetTitle()
        => State.Title;

    protected override void OnClick()
        => MessageBannerMediator.RestoreMessageBanner();

    protected override void OnStateChanged(NotificationElementStateChangedEventArgs args)
    {
        if (args.PropertyNames.Contains(nameof(MessageBannerNotificationElementState.Title)))
            InvokeAsync(StateHasChanged);
        else
            base.OnStateChanged(args);
    }
}
