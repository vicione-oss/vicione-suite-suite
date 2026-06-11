using Sdk.Messaging;

namespace Core.Shared.Passkeys.Commands;

public sealed record DeletePasskeys : ICommand
{
    public required string UserId { get; init; }

    public required IEnumerable<string> PasskeyIds { get; init; }

    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
