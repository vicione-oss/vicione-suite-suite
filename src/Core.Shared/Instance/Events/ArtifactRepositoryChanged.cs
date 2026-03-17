using Core.Shared.Instance.Contracts;
using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.Instance.Events;

[ForwardToUI]
public sealed record ArtifactRepositoryChanged(ArtifactRepository Repository, CrudAction Action, ErrorInfo? Error = null) : IEvent, CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
