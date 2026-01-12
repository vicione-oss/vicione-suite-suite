using ViciOne.Journal;

namespace Core.OS.Monitoring;

internal sealed class SessionManager(Func<CancellationToken, Task<IJournalGenericSession>> createGenericSession, string filter, IReadOnlyCollection<string> fields, StringComparer stringComparer)
{
    private string? _lastCursor;

    private readonly Dictionary<string, int> _countsBuffer = new(stringComparer);
    private readonly Dictionary<string, int> _currentCounts = new(stringComparer);

    public async Task Process(DateTimeOffset cycleStartTime, CancellationToken cancellationToken)
    {
        using var session = await createGenericSession(cancellationToken);

        session.SetFilter(filter);
        var positionIsCursor = SetPosition();

        foreach (var entry in session.GetLogs(int.MaxValue, true, positionIsCursor))
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var field in entry)
            {
                if (!fields.Contains(field.Key))
                    continue;

                var pair = $"{field.Key}={field.Value}";

                Increase(_currentCounts, pair);
                Increase(_countsBuffer, pair);
            }
        }

        if (_currentCounts.Count > 0)
            _lastCursor = session.GetCursor();

        bool SetPosition()
        {
            if (string.IsNullOrEmpty(_lastCursor))
            {
                session.SeekRealTime(cycleStartTime.UtcDateTime.AddMinutes(-1));
                return false;
            }
            else
            {
                session.SeekCursor(_lastCursor);
                return true;
            }
        }
    }

    public Dictionary<string, int> GetCurrentCounts()
    {
        Dictionary<string, int> counts = new(_currentCounts, stringComparer);
        _currentCounts.Clear();
        return counts;
    }

    public Dictionary<string, int> GetCountsBuffer()
    {
        Dictionary<string, int> counts = new(_countsBuffer, stringComparer);
        _countsBuffer.Clear();
        return counts;
    }

    private static void Increase(Dictionary<string, int> counts, string pair)
    {
        if (!counts.TryAdd(pair, 1))
            counts[pair]++;
    }
}
