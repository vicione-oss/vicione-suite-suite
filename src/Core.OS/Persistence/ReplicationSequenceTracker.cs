using System.Collections.Concurrent;

namespace Core.OS.Persistence;

/// <summary>
///     In-memory reorder buffer for the slave-side replication consumer.
///     Buffers out-of-order <see cref="DbChangeSet" /> messages and drains them once the gap fills.
///     If the gap persists beyond a timeout (or the buffer overflows), flushes all buffered messages
///     and signals that a full-sync is required.
/// </summary>
public sealed class ReplicationSequenceTracker(TimeProvider timeProvider)
{
    private const int MaxBufferSize = 1000;
    private static readonly TimeSpan DefaultBufferTimeout = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<string, ContextState> _states = new(StringComparer.Ordinal);

    /// <summary>
    ///     Parameterless ctor for DI — uses <see cref="TimeProvider.System" />. Tests inject <c>FakeTimeProvider</c> via the
    ///     primary constructor.
    /// </summary>
    public ReplicationSequenceTracker() : this(TimeProvider.System) { }

    public long GetLastApplied(string contextType) =>
        _states.TryGetValue(contextType, out var state) ? state.LastApplied : 0;

    public int GetBufferedCount(string contextType) =>
        _states.TryGetValue(contextType, out var state) ? state.Pending.Count : 0;

    /// <summary>
    ///     Returns last-applied sequence numbers for all known context types.
    ///     Used during slave registration to report replication position to the master (ADR-003 Gap 4).
    /// </summary>
    public IReadOnlyDictionary<string, long> GetAllLastApplied() =>
        _states.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.LastApplied);

    /// <summary>
    ///     Submits a message to the tracker. Returns a result indicating which messages
    ///     (if any) should be applied and whether a full-sync is required.
    /// </summary>
    public ReplicationSequenceResult Submit(string contextType, long sequenceNumber, DbChangeSet message)
    {
        var state = _states.GetOrAdd(contextType, _ => new ContextState());

        // First message ever or after a reset: accept and establish baseline
        if (state.LastApplied == 0)
        {
            state.LastApplied = sequenceNumber;
            return ReplicationSequenceResult.Apply([message]);
        }

        // Duplicate or already-applied: skip
        if (sequenceNumber <= state.LastApplied)
            return ReplicationSequenceResult.None;

        // Next expected in sequence: apply + drain any contiguous buffered messages
        if (sequenceNumber == state.LastApplied + 1)
        {
            state.LastApplied = sequenceNumber;
            var toApply = new List<DbChangeSet> { message };
            DrainContiguous(state, toApply);
            return ReplicationSequenceResult.Apply(toApply);
        }

        // Ahead of expected: buffer and check for timeout/overflow
        state.Pending[sequenceNumber] = message;
        state.FirstBufferedAt ??= timeProvider.GetUtcNow();

        if (ShouldFlush(state))
        {
            var flushed = Flush(state);
            return ReplicationSequenceResult.ApplyAndResync(flushed);
        }

        return ReplicationSequenceResult.Buffered;
    }

    /// <summary>
    ///     Resets tracking for all contexts. Called after a full-sync completes.
    /// </summary>
    public void Reset() => _states.Clear();

    /// <summary>
    ///     Resets tracking for a specific context type.
    /// </summary>
    public void Reset(string contextType) => _states.TryRemove(contextType, out _);

    private static void DrainContiguous(ContextState state, List<DbChangeSet> target)
    {
        while (state.Pending.Remove(state.LastApplied + 1, out var buffered))
        {
            state.LastApplied++;
            target.Add(buffered);
        }

        if (state.Pending.Count == 0)
            state.FirstBufferedAt = null;
    }

    private bool ShouldFlush(ContextState state) =>
        state.Pending.Count >= MaxBufferSize
        || (state.FirstBufferedAt.HasValue
            && timeProvider.GetUtcNow() - state.FirstBufferedAt.Value >= DefaultBufferTimeout);

    private static List<DbChangeSet> Flush(ContextState state)
    {
        var flushed = new List<DbChangeSet>(state.Pending.Count);
        foreach (var (seq, msg) in state.Pending)
        {
            flushed.Add(msg);
            state.LastApplied = seq;
        }

        state.Pending.Clear();
        state.FirstBufferedAt = null;
        return flushed;
    }

    private sealed class ContextState
    {
        public long LastApplied;
        public SortedDictionary<long, DbChangeSet> Pending = new();
        public DateTimeOffset? FirstBufferedAt;
    }
}
