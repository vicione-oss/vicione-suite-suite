using System.Diagnostics.CodeAnalysis;
using JiTChat.Client.Contracts;
using Microsoft.AspNetCore.Components;
using Sdk.Client.NotificationArea.Components;

namespace JiTChat.Client.NotificationArea;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by DI container")]
internal sealed class JiTChatNotificationElementBadge : NotificationElementNumberBadgeBase, IDisposable
{
    [Inject] public required IJiTChatService ChatService { get; set; }

    protected override void OnInitialized()
        => ChatService.OnUnreadMessagesChanged += UnreadMessagesChanged;

    public void Dispose()
    {
        ChatService.OnUnreadMessagesChanged -= UnreadMessagesChanged;

        GC.SuppressFinalize(this);
    }

    private void UnreadMessagesChanged() => InvokeAsync(StateHasChanged);

    protected override int? GetNumber() => ChatService.UnreadMessages > 0 ? ChatService.UnreadMessages : null;
}
