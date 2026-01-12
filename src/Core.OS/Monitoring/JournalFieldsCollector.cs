using Core.Shared.Monitoring;
using Microsoft.Extensions.Options;
using Sdk.Journal;
using ViciOne.Journal;
using ViciOne.SystemMonitoring.Collectors;
using ViciOne.SystemMonitoring.Metrics;

namespace Core.OS.Monitoring;

public sealed class JournalFieldsCollector(IOptions<JournalFieldsCollectorOptions> options, IJournalMonitoring journalMonitoring,
    Func<CancellationToken, Task<IJournalGenericSession>> createGenericSession) : IScheduledMetricsCollector
{
    private readonly Dictionary<string, SessionManager> _sessionManagers = [];

    public int IntervalInMinutes => 1;
    public string Name => Collectors.JournalFields;

    public JournalFieldsCollector(IOptions<JournalFieldsCollectorOptions> options, IJournalMonitoring journalMonitoring, JournalService journalService)
        : this(options, journalMonitoring, async c => await journalService.CreateGeneric(c).ConfigureAwait(false))
    { }

    public async Task<List<Metric>> CollectMetrics(int interval, DateTimeOffset cycleStartTime, CancellationToken cancellationToken)
    {
        List<Metric> metrics = [];

        foreach (var (name, entry) in journalMonitoring.FilterEntries)
        {
            if (!_sessionManagers.TryGetValue(name, out var sessionManager))
            {
                sessionManager = new(
                    createGenericSession,
                    entry.Filter,
                    options.Value.Fields,
                    StringComparer.OrdinalIgnoreCase);
                _sessionManagers[name] = sessionManager;
            }

            await sessionManager.Process(cycleStartTime, cancellationToken).ConfigureAwait(false);

            metrics.Add(new()
            {
                Name = JournalFieldsMetric.Count,
                Group = Name,
                SubGroup = name,
                IntervalInMinutes = IntervalInMinutes,
                Value = sessionManager.GetCurrentCounts(),
                Unit = "entries"
            });

            if (interval % 15 == 0)
            {
                metrics.Add(new()
                {
                    Name = JournalFieldsMetric.Count,
                    Group = Name,
                    SubGroup = name,
                    IntervalInMinutes = 15,
                    Value = sessionManager.GetCountsBuffer(),
                    Unit = "entries"
                });
            }
        }

        Cleanup();

        return metrics;

        void Cleanup()
        {
            var sessionManagersToRemove = _sessionManagers.Select(e => e.Key).Except(journalMonitoring.FilterEntries.Keys).ToArray();

            foreach (var name in sessionManagersToRemove)
                _sessionManagers.Remove(name);
        }
    }
}
