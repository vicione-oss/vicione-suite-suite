using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.HostManagement;

[ForwardToUI]
public record InstallSuiteVersionStarted(Guid CorrelationId, string? Message, bool WithWarnings) : IEvent, CorrelatedBy<Guid>;
