using JiTChat.Public.Contracts;
using Sdk.Messaging;

namespace JiTChat.Public.Commands;

public sealed record PublishMessage : ICommand
{
    public Guid SenderId { get; set; }
    public ChatMessage Message { get; set; } = new();
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
