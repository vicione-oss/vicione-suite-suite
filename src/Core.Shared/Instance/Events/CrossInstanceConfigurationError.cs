using MassTransit;
using Sdk.Messaging;

namespace Core.Shared.Instance.Events;

[ForwardToUI]
public sealed record CrossInstanceConfigurationError(Guid CorrelationId, ErrorInfo Error, Guid? CrossInstanceConfigurationId) : IEvent, CorrelatedBy<Guid>
{
    public const int UnknownError = 0;
    public const int AddOrUpdateFailed = 200;
}
