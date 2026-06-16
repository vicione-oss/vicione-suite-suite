namespace Core.OS.Instance.Services;

/// <summary>
/// Tracks full-sync routing slip retry attempts on slave instances.
/// When retries are exhausted, the slave is marked as degraded.
/// </summary>
public sealed class SyncRetryState
{
    public const int DefaultMaxRetries = 3;
    public static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(10);

    private readonly Lock _lock = new();
    private int _attemptCount;
    private bool _degraded;

    public int MaxRetries { get; init; } = DefaultMaxRetries;

    public TimeSpan RetryDelay { get; init; } = DefaultRetryDelay;

    /// <summary>
    /// Current number of consecutive failed sync attempts.
    /// </summary>
    public int AttemptCount
    {
        get { lock (_lock) return _attemptCount; }
    }

    /// <summary>
    /// True when retry limit has been exhausted and the slave is degraded.
    /// </summary>
    public bool IsDegraded
    {
        get { lock (_lock) return _degraded; }
    }

    /// <summary>
    /// Records a failed sync attempt. Returns true if a retry is allowed, false if retries are exhausted.
    /// </summary>
    public bool RecordFailure()
    {
        lock (_lock)
        {
            _attemptCount++;
            if (_attemptCount >= MaxRetries)
            {
                _degraded = true;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Resets state after a successful synchronization.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _attemptCount = 0;
            _degraded = false;
        }
    }
}
