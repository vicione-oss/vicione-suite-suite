using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.HostManagement.Events;

[ForwardToUI]
public record SystemRestartRequired(Guid CorrelationId, RestartReason Reason) : IEvent, CorrelatedBy<Guid>;

public enum RestartReason
{
    SystemConfiguration,
    ModuleConfiguration,
}
