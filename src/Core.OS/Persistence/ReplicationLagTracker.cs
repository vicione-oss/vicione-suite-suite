using System.Collections.Concurrent;

namespace Core.OS.Persistence;

/// <summary>
///     Tracks replication lag per context type on slave instances.
///     The lag is calculated as the difference between the slave's local time and the
///     <see cref="DbChangeSet.PublishedAt" /> timestamp from the master.
///     NTP synchronization between master and slave is assumed.
/// </summary>
public sealed class ReplicationLagTracker(TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, TimeSpan> _lags = new(StringComparer.Ordinal);

    /// <summary>
    ///     Parameterless ctor for DI — uses <see cref="TimeProvider.System" />. Tests inject <c>FakeTimeProvider</c> via the
    ///     primary constructor.
    /// </summary>
    public ReplicationLagTracker() : this(TimeProvider.System) { }

    public void Record(string contextType, DateTimeOffset publishedAt)
    {
        var lag = timeProvider.GetUtcNow() - publishedAt;
        _lags[contextType] = TimeSpan.FromTicks(Math.Max(0, lag.Ticks));
    }

    public TimeSpan? GetLag(string contextType) =>
        _lags.TryGetValue(contextType, out var lag) ? lag : null;

    public IReadOnlyDictionary<string, TimeSpan> GetAllLags() =>
        new Dictionary<string, TimeSpan>(_lags);
}
