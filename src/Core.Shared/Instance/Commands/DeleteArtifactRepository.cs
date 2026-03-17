using Sdk.Messaging;

namespace Core.Shared.Instance.Commands;

public sealed record DeleteArtifactRepository(Guid RepositoryId) : ICommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
