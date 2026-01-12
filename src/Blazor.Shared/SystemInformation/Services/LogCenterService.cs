using Blazor.Shared.SystemInformation.Models;
using Core.Shared.Monitoring;
using Sdk.Journal;
using ViciOne.SystemMonitoring;
using ViciOne.SystemMonitoring.Metrics;

namespace Blazor.Shared.SystemInformation.Services;

public class LogCenterService : IOutput
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, List<LogCenterEntry>> _buffers = [];
    private readonly Dictionary<string, LogCenterEntry> _summaries = [];
    private readonly IJournalMonitoring _journalMonitoring;

    public IReadOnlyDictionary<string, LogCenterEntry> LogCenterEntries => Lock(() => _summaries.ToDictionary());

    public event Action? Changed;

    public LogCenterService(IJournalMonitoring journalMonitoring)
    {
        _journalMonitoring = journalMonitoring;

        foreach (var key in _journalMonitoring.FilterEntries.Keys)
            _summaries[key] = new(0, 0, 0);
    }

    public Task Connect(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task Disconnect(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task Publish(IReadOnlyCollection<Metric> metrics, DateTimeOffset timestamp, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _summaries.Clear();

            foreach (var key in _journalMonitoring.FilterEntries.Keys)
            {
                var metric = metrics.FirstOrDefault(m => m.Group == Collectors.JournalFields && m.Name == JournalFieldsMetric.Count && m.SubGroup == key && m.IntervalInMinutes == 1);

                if (metric is not null)
                {
                    var counts = metric.Value as Dictionary<string, int> ?? [];
                    var entry = CalculateCount(counts);

                    if (!_buffers.TryGetValue(key, out var buffer))
                    {
                        buffer = [];
                        _buffers[key] = buffer;
                    }

                    buffer.Add(entry);

                    if (buffer.Count > 15)
                        buffer.RemoveAt(0);

                    _summaries[key] = new LogCenterEntry(
                        buffer.Select(e => e.InfoCount).Sum(),
                        buffer.Select(e => e.WarningCount).Sum(),
                        buffer.Select(e => e.ErrorCount).Sum());
                }
            }
        }

        Changed?.Invoke();

        return Task.CompletedTask;
    }

    private static LogCenterEntry CalculateCount(Dictionary<string, int> counts)
    {
        var info = 0;
        var warnings = 0;
        var errors = 0;

        foreach (var entry in counts)
        {
            switch (entry.Key)
            {
                case JournalPriorityConstants.Emergency:
                case JournalPriorityConstants.Alert:
                case JournalPriorityConstants.Critical:
                case JournalPriorityConstants.Error:
                    errors += entry.Value;
                    break;
                case JournalPriorityConstants.Warning:
                    warnings += entry.Value;
                    break;
                case JournalPriorityConstants.Notice:
                case JournalPriorityConstants.Information:
                case JournalPriorityConstants.Debug:
                    info += entry.Value;
                    break;
            }
        }

        return new(info, warnings, errors);
    }

    private T Lock<T>(Func<T> get)
    {
        lock (_lock)
            return get();
    }
}
