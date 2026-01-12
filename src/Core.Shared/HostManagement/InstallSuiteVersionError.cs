using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.HostManagement;

[ForwardToUI]
public record InstallSuiteVersionError(Guid CorrelationId, ErrorInfo Error) : IEvent, CorrelatedBy<Guid>;
