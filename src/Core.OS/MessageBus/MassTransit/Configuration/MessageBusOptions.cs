using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using MassTransit;
using Microsoft.Extensions.Options;

namespace Core.OS.MessageBus.MassTransit.Configuration;

[SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "DTO")]
public sealed class MessageBusOptions
{
    public const string ConfigSection = "MessageBus";
    public const string TransportOptionsConfigSection = ConfigSection + ":Connection";

    public bool UseInMemoryBus { get; set; }

    /// <summary>
    /// Time until a queue is deleted after the last message
    /// </summary>
    [Range(0, 5)]
    public int QueueLifetimeInDays { get; set; } = 5;

    /// <summary>
    /// Gets or sets the retry intervals if some problem with the connection occurs
    /// </summary>
    public int[] RetryIntervals { get; set; } = [100, 500, 1000, 2000];

    /// <summary>
    /// Gets or sets the maximal amount of unacked messages a consumer can prefetch
    /// </summary>
    public ushort? PrefetchCount { get; set; }

    public bool DurableQueues { get; set; } = true;

    /// <summary>
    /// ConnectionOptions For RabbitMq
    /// </summary>
    [ValidateObjectMembers]
    public RabbitMqTransportOptions Connection { get; set; } = new();

    /// <summary>
    /// Deletes all queues and exchanges from the broker
    /// </summary>
    public bool CleanVirtualHost { get; set; }
}
