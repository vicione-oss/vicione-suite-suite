using JiTChat.Client.Contracts;
using JiTChat.Public.Commands;
using JiTChat.Public.Contracts;
using JiTChat.Public.Events;
using Sdk.Client.Infrastructure;

namespace JiTChat.Client.Services;

public sealed class JiTChatService : IJiTChatService, IDisposable
{
    private readonly Guid _id = Guid.NewGuid();
    private readonly IUiMediator _mediator;
    private readonly IDisposable _subscriptionHandle;

    public List<ChatMessage> ChatMessages { get; } = [];
    public int UnreadMessages { get; private set; }
    public bool MarkIncomingMessagesAsRead { get; set; }

    public event Action<ChatMessage>? OnMessagePublished;
    public event Action? OnUnreadMessagesChanged;

    public JiTChatService(IUiMediator mediator)
    {
        _mediator = mediator;
        _subscriptionHandle = _mediator.Register(this);
    }

    public Task Consume(ClientContext<MessagePublished> context, CancellationToken cancellationToken)
    {
        ChatMessages.Add(context.Message.Message);
        OnMessagePublished?.Invoke(context.Message.Message);

        if (context.Message.SenderId != _id && !MarkIncomingMessagesAsRead)
        {
            UnreadMessages++;
            OnUnreadMessagesChanged?.Invoke();
        }

        return Task.CompletedTask;
    }

    public void Dispose() => _subscriptionHandle.Dispose();

    public async Task SendMessage(string user, string messageText)
    {
        await _mediator.Send(new PublishMessage
        {
            SenderId = _id,
            Message = new()
            {
                Message = messageText,
                User = user,
            }
        });
    }

    public void MarkAllMessagesAsRead()
    {
        UnreadMessages = 0;

        OnUnreadMessagesChanged?.Invoke();
    }
}
