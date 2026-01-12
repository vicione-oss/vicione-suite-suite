using MassTransit;
using Sdk.Backend.Messaging;

namespace Core.OS.MessageBus.MassTransit;

internal sealed class RoutingSlipBuilderFactory : IRoutingSlipBuilderFactory
{
    public IRoutingSlipBuilder Create(Guid trackingNumber) => new RoutingSlipBuilder(trackingNumber);
}
