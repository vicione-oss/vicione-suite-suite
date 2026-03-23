using Core.Shared.Modules.Contracts;
using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.Modules.Events;

[ForwardToUI]
public sealed record ModulePackageOperationsChanged(IReadOnlyList<ModulePackageChange> Changes, ErrorInfo? Error = null) : IEvent, CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
