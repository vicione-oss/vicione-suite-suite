using MassTransit;
using Sdk.Instance;
using Sdk.Messaging;

namespace Core.Shared.Instance.Events;

[ForwardToUI]
public sealed record InstanceInformationUpdated(Guid CorrelationId, IInstanceInformation InstanceInformation, ErrorInfo? Error = null) : IEvent, CorrelatedBy<Guid>;
