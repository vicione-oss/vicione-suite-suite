namespace Core.OS.Persistence;

/// <summary>
///     Persisted state for the master-side replication sequence counter.
///     Stored in the outbox schema (master-only, PostgreSQL).
///     Ensures the counter survives master restarts without re-issuing sequence numbers.
/// </summary>
public sealed class ReplicationSequenceState
{
    public required string ContextType { get; set; }
    public long LastSequenceNumber { get; set; }
}
