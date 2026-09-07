namespace Core.OS.MessageBus.MassTransit.Configuration;

/// <summary>
/// Message classes that get their own retry ladder. See ADR-004 (D2).
/// </summary>
internal enum MessageRetryClass
{
    /// <summary>
    /// Instance-independent commands, events and activities, plus anything unrecognized. These share the
    /// <c>Commands</c> and <c>Events</c> queues rather than owning an endpoint each, so a retry here stalls every
    /// other message type on the same queue and the ladder has to stay short.
    /// </summary>
    Default,

    /// <summary>
    /// The single, strictly serialized instance queue that carries the replication stream together with every
    /// instance-dependent command. A long ladder here blocks the whole node, so this class retries briefly and lets
    /// full sync (ADR-003) repair a replication gap.
    /// </summary>
    SerializedInstance,

    /// <summary>
    /// Request/response, where a caller is blocked on its request timeout. Short ladder, so the caller sees the actual
    /// fault instead of a timeout.
    /// </summary>
    Request
}
