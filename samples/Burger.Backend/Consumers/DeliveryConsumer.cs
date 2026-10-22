using Burger.Internal.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Burger.Backend.Consumers;

public sealed partial class DeliveryConsumer(ILogger<DeliveryConsumer> logger) : IConsumer<DeliveryCommand>
{
    public async Task Consume(ConsumeContext<DeliveryCommand> context)
    {
        LogStartingDelivery(logger, context.Message.OrderId);

        // Delivery takes some time.
        await Task.Delay(3000);

        // Publishing the event moves the saga to its final state.
        await context.Publish(new DeliveryCompleted(context.Message.OrderId));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting delivery of order {OrderId}")]
    private static partial void LogStartingDelivery(ILogger logger, Guid orderId);
}
