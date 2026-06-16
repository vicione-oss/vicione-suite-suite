using System.Collections.Concurrent;

namespace Core.OS.Persistence;

/// <summary>
///     Assigns monotonically increasing sequence numbers per context type on the master.
///     Singleton — injected into the scoped <see cref="ChangeTrackingInterceptor"/> to stamp each <see cref="DbChangeSet"/>.
///     Seeded from PostgreSQL on startup to survive master restarts (ADR-003 Gap 4).
/// </summary>
public sealed class ReplicationSequenceCounter
{
    private readonly ConcurrentDictionary<string, long> _counters = new(StringComparer.Ordinal);

    public long Next(string contextType) =>
        _counters.AddOrUpdate(contextType, _ => 1, (_, current) => current + 1);

    public long Current(string contextType) =>
        _counters.GetValueOrDefault(contextType);

    /// <summary>
    ///     Seeds the counter for a context type. Used on master startup to restore persisted state.
    ///     Only seeds if the provided value is higher than the current value (prevents regression).
    /// </summary>
    public void Seed(string contextType, long value)
    {
        _counters.AddOrUpdate(contextType, _ => value, (_, current) => Math.Max(current, value));
    }

    /// <summary>
    ///     Returns all known context types and their current sequence numbers.
    ///     Used during slave registration to compare against slave-reported sequences.
    /// </summary>
    public IReadOnlyDictionary<string, long> GetAll() =>
        _counters.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
}
