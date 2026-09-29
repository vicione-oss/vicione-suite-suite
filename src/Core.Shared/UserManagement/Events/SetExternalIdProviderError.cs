using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.UserManagement.Events;

[ForwardToUI]
public sealed record SetExternalIdProviderError(Guid CorrelationId, ErrorInfo Error) : IEvent, CorrelatedBy<Guid>;
