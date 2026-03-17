using Core.Shared.Instance.Contracts;
using Sdk.Messaging;

namespace Core.Shared.Instance.Commands;

public sealed record UpdateArtifactRepository(ArtifactRepository Repository) : ICommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
