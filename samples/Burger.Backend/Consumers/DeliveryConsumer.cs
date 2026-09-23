using Burger.Internal.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Burger.Backend.Consumers;

public sealed class DeliveryConsumer(ILogger<DeliveryConsumer> logger) : IConsumer<DeliveryCommand>
{
    public async Task Consume(ConsumeContext<DeliveryCommand> context)
    {
        logger.LogInformation("Starting delivery of order {OrderId}", context.Message.OrderId);

        // Delivery takes some time.
        await Task.Delay(3000);

        // Publishing the event moves the saga to its final state.
        await context.Publish(new DeliveryCompleted(context.Message.OrderId));
    }
}
