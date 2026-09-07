using MassTransit;

namespace Core.OS.MessageBus.MassTransit.Configuration;

/// <summary>
/// Bounds the lazily declared <c>_error</c> and <c>_skipped</c> queues. See ADR-004 (D5).
/// </summary>
/// <remarks>
/// MassTransit builds both from the input endpoint's settings and copies its queue arguments, which leaves them
/// unbounded on a master — that node deliberately has no queue expiration. These arguments are applied on top.
/// </remarks>
internal static class FaultQueueTopology
{
    /// <summary>
    /// Applies the configured bounds to one fault queue. A setting of <c>0</c> omits the corresponding argument, which
    /// is the escape hatch for a broker that already declared the queue with different arguments — redeclaring a
    /// RabbitMQ queue with changed arguments fails with <c>PRECONDITION_FAILED</c>.
    /// </summary>
    public static void Configure(IRabbitMqQueueBindingConfigurator queue, ErrorQueueSettings settings)
    {
        if (settings.RetentionInDays > 0)
        {
            var retention = TimeSpan.FromDays(settings.RetentionInDays);
            queue.SetQueueArgument("x-message-ttl", (long)retention.TotalMilliseconds);
            queue.QueueExpiration = retention;
        }

        if (settings.MaxMessages > 0)
            queue.SetQueueArgument("x-max-length", (long)settings.MaxMessages);

        if (settings.MaxSizeInMegabytes > 0)
            queue.SetQueueArgument("x-max-length-bytes", (long)settings.MaxSizeInMegabytes * 1024 * 1024);

        // Dropping the oldest fault is required: reject-publish would make the error transport itself fail and turn a
        // poison message into a redelivery loop.
        if (settings.MaxMessages > 0 || settings.MaxSizeInMegabytes > 0)
            queue.SetQueueArgument("x-overflow", "drop-head");
    }
}
