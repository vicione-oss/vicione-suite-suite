using ViciOne.Journal;

namespace Core.OS.Monitoring;

/// <summary>
/// This service will be used in scenarios where journal functionality is not available (Windows)
/// </summary>
internal sealed class NoOpJournalService : IJournalService
{
    public Task<IJournalFollowSession> CreateFollower(TimeSpan interval, CancellationToken cancellation = default)
        => Task.FromResult<IJournalFollowSession>(new NoOpJournalFollowSession());

    public Task<IJournalGenericSession> CreateGeneric(CancellationToken cancellationToken = default)
        => Task.FromResult<IJournalGenericSession>(new NoOpJournalGenericSession());

    public void Dispose() { }

    public IReadOnlyDictionary<string, string> GetFieldDescriptions() => new Dictionary<string, string>();

    public Task<IReadOnlyCollection<string>> GetFields(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<string>>([]);

    public Task<IReadOnlyCollection<string>> GetFieldValues(string field, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<string>>([]);

    public Task<IReadOnlyCollection<IReadOnlyDictionary<string, string>>> GetLogs(DateTimeOffset since, string order = "desc", int limit = 100, string? filter = null, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<IReadOnlyDictionary<string, string>>>([]);

    public Task<IReadOnlyDictionary<int, int>> GetPriorityCounts(DateTimeOffset since, string? filter = null, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<int, int>>(new Dictionary<int, int>());
    public Task<IReadOnlyDictionary<int, int>> GetPriorityCounts(DateTimeOffset start, DateTimeOffset end, string? filter = null, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<int, int>>(new Dictionary<int, int>());

    private class NoOpJournalGenericSession : IJournalGenericSession
    {
        public void Dispose() { }
        public string GetCursor() => string.Empty;
        public IEnumerable<IReadOnlyDictionary<string, string>> GetLogs(int size, bool forward = true, bool skipFirst = false) => [];
        public void SeekCursor(string cursor) { }
        public void SeekHead() { }
        public void SeekRealTime(DateTime timestamp) { }
        public void SeekTail() { }
        public void SetFilter(string filter) { }
    }

    private class NoOpJournalFollowSession : IJournalFollowSession
    {
        public event Action<Exception>? Error;
        public event Action<IReadOnlyDictionary<string, string>>? NewEntry;

        public Task<IReadOnlyCollection<IReadOnlyDictionary<string, string>>> GetNext(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<IReadOnlyDictionary<string, string>>>([]);
        public void Dispose() { }
        public void SetFilter(string filter) { Error?.Invoke(new InvalidOperationException()); }
        public void Start(int logCount) { NewEntry?.Invoke(new Dictionary<string, string>()); }
        public Task Stop() => Task.CompletedTask;
    }
}
