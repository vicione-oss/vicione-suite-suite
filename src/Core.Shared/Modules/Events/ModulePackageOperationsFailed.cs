using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.Modules.Events;

[ForwardToUI]
public sealed record ModulePackageOperationsFailed(Guid CorrelationId, ErrorInfo Error) : IEvent, CorrelatedBy<Guid>;
