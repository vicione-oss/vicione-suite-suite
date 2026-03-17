using Core.Shared.Instance.Contracts;
using Sdk.Messaging;

namespace Core.Shared.Instance.Commands;

public sealed record UpdateArtifactRepositoryToken(ArtifactRepository Repository) : ICommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
