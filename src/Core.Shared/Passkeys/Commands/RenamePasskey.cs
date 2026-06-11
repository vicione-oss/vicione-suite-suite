using Sdk.Messaging;

namespace Core.Shared.Passkeys.Commands;

public sealed record RenamePasskey : ICommand
{
    public required string UserId { get; init; }

    public required string PasskeyId { get; init; }

    public required string NewName { get; init; }

    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
