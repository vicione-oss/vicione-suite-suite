using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.UserManagement.Events;

[ForwardToUI]
public sealed record ExternalIdProviderChanged(Guid CorrelationId) : IEvent, CorrelatedBy<Guid>;
