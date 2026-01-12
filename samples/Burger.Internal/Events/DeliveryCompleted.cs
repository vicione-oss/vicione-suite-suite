using MassTransit;
using Sdk.Messaging;

namespace Burger.Internal.Events;

public sealed record DeliveryCompleted(Guid OrderId) : IEvent, CorrelatedBy<Guid>
{
    public Guid CorrelationId => OrderId;
}
