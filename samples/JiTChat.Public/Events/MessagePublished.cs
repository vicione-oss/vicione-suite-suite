using JiTChat.Public.Contracts;
using Sdk.Messaging;

namespace JiTChat.Public.Events;

[ForwardToUI]
public sealed record MessagePublished : IEvent
{
    public Guid SenderId { get; set; }
    public ChatMessage Message { get; set; } = new();
}
