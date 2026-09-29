using Sdk.Messaging;

namespace Core.Shared.UserManagement.Commands;

/// <summary>
/// Stores the single OpenID provider, or removes it when both <see cref="Authority"/> and
/// <see cref="ClientId"/> are empty.
/// </summary>
public sealed record SetExternalIdProvider : ICommand
{
    public required string Authority { get; init; }

    public required string ClientId { get; init; }

    public required ClientSecretUpdate ClientSecret { get; init; }

    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
