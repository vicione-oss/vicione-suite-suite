using System.Collections.Concurrent;
using Sdk.Journal;

namespace Core.OS.Monitoring;

public class JournalMonitoring : IJournalMonitoring
{
    private readonly ConcurrentDictionary<string, JournalFilterEntry> _targets = [];

    public IReadOnlyDictionary<string, JournalFilterEntry> FilterEntries => _targets;

    public void AddFilterEntry(string name, string displayName, string filter, int index)
        => _targets.AddOrUpdate(name,
            _ => new(displayName, filter, index),
            (_, _) => new(displayName, filter, index));
}
