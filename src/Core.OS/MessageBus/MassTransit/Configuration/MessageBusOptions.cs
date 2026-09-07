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
    /// Gets or sets the retry intervals in milliseconds for commands, events and everything that is not classified
    /// otherwise. See ADR-004 (D2). Kept short because those messages share the serialized <c>Commands</c> and
    /// <c>Events</c> queues, so every retry stalls the other message types queued behind it.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> — the property is not configured at all — means
    /// <see cref="MessageRetryClassifier.DefaultIntervals"/>. An <b>empty</b> array disables retry for these endpoints:
    /// a message is attempted once and then faults. Configure it as <c>[]</c> in JSON, or as an environment variable
    /// with an empty value (<c>MessageBus__RetryIntervals=</c>).
    /// <para>
    /// The default deliberately lives in <see cref="MessageRetryClassifier"/> rather than on this property, and the
    /// property is nullable rather than defaulting to an empty array for the same reason: the configuration binder
    /// <b>appends</b> configured array elements to a non-empty default instead of replacing them, and only a
    /// <see langword="null"/> default keeps an empty array free to mean "no retry".
    /// </para>
    /// </remarks>
    public int[]? RetryIntervals { get; set; }

    /// <summary>
    /// Gets or sets the retry intervals in milliseconds for the single serialized instance queue, which carries the
    /// replication stream and every instance-dependent command. See ADR-004 (D2). Kept short because that queue is
    /// consumed one message at a time, so every retry blocks the whole node.
    /// <see langword="null"/> means <see cref="MessageRetryClassifier.SerializedInstanceIntervals"/>, an empty array
    /// disables retry — see <see cref="RetryIntervals"/>.
    /// </summary>
    public int[]? InstanceQueueRetryIntervals { get; set; }

    /// <summary>
    /// Gets or sets the retry intervals in milliseconds for request/response consumers. See ADR-004 (D2). Must stay
    /// well below the caller's request timeout so a fault is reported instead of a timeout.
    /// <see langword="null"/> means <see cref="MessageRetryClassifier.RequestIntervals"/>, an empty array disables
    /// retry — see <see cref="RetryIntervals"/>.
    /// </summary>
    public int[]? RequestRetryIntervals { get; set; }

    /// <summary>
    /// Gets or sets the maximal amount of unacked messages a consumer can prefetch
    /// </summary>
    public ushort? PrefetchCount { get; set; }

    public bool DurableQueues { get; set; } = true;

    /// <summary>
    /// Stops a receive endpoint while its dependencies are failing. See ADR-004 (D4).
    /// </summary>
    [ValidateObjectMembers]
    public KillSwitchSettings KillSwitch { get; set; } = new();

    /// <summary>
    /// Bounds the broker side retention of faulted and skipped messages. See ADR-004 (D5).
    /// </summary>
    [ValidateObjectMembers]
    public ErrorQueueSettings ErrorQueue { get; set; } = new();

    /// <summary>
    /// ConnectionOptions for RabbitMq. Deliberately not validated: MassTransit owns this type and carries no
    /// validation attributes on it, and the suite does not annotate types it does not own. A wrong host, port or
    /// credential surfaces as a broker connection failure at bus start. Recorded in ADR-004.
    /// </summary>
    public RabbitMqTransportOptions Connection { get; set; } = new();

    /// <summary>
    /// Deletes all queues and exchanges from the broker
    /// </summary>
    public bool CleanVirtualHost { get; set; }
}
