using Sdk.Messaging;

namespace Core.Shared.UserManagement.Commands;

public sealed record DeleteExternalLogin : ICommand
{
    public required string UserId { get; init; }

    public required string LoginProvider { get; init; }

    public required string ProviderKey { get; init; }

    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
