using Core.Shared.Modules.Contracts;
using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.Modules.Events;

/// <summary>
/// Per-node event reporting that a single instance enqueued module package operations. Published by
/// every node (including slaves) so the backend can correlate the outcome per instance. Unlike
/// <see cref="ModulePackageOperationsChanged"/> it is not forwarded to the UI; it is intended as the
/// foundation for a future saga orchestrating the cluster-wide update and restart.
/// </summary>
public sealed record ModulePackageOperationsEnqueued(Guid InstanceId, IReadOnlyList<ModulePackageChange> Changes, ErrorInfo? Error = null) : IEvent, CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
