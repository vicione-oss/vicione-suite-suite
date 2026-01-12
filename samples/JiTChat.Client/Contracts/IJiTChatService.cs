using JiTChat.Public.Contracts;
using JiTChat.Public.Events;
using Sdk.Client.Infrastructure;

namespace JiTChat.Client.Contracts;

public interface IJiTChatService : IEventConsumer<MessagePublished>
{
    bool MarkIncomingMessagesAsRead { get; set; }
    int UnreadMessages { get; }
    List<ChatMessage> ChatMessages { get; }

    event Action<ChatMessage> OnMessagePublished;
    event Action OnUnreadMessagesChanged;

    Task SendMessage(string user, string messageText);

    void MarkAllMessagesAsRead();
}
