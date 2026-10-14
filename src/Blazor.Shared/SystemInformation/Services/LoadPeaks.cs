using Blazor.Shared.SystemInformation.Models;

namespace Blazor.Shared.SystemInformation.Services;

/// <summary>
/// Finds the stretches of minutes in which CPU, temperature or memory pressure stayed above their warning threshold.
/// </summary>
internal static class LoadPeaks
{
    /// <summary>
    /// Minutes before and after the highest one that the course of a peak shows.
    /// </summary>
    private const int CourseMinutes = 10;

    /// <summary>
    /// The peaks of all values, newest first.
    /// </summary>
    public static IReadOnlyList<LoadPeak> Find(IReadOnlyList<MinuteLoad> minutes)
        => [.. Find(minutes, PeakKind.Cpu, m => m.Cpu, ProcessOverviewThresholds.CpuPercent)
            .Concat(Find(minutes, PeakKind.Temperature, m => m.Temperature, ProcessOverviewThresholds.TemperatureCelsius))
            .Concat(Find(minutes, PeakKind.MemoryPressure, m => m.Refaults, ProcessOverviewThresholds.RefaultsPerSecond))
            .OrderByDescending(peak => peak.PeakTime)];

    private static IEnumerable<LoadPeak> Find(IReadOnlyList<MinuteLoad> minutes, PeakKind kind, Func<MinuteLoad, double?> value, double limit)
    {
        for (var i = 0; i < minutes.Count; i++)
        {
            if (!(value(minutes[i]) > limit))
                continue;

            var start = i;
            while (i + 1 < minutes.Count && value(minutes[i + 1]) > limit)
                i++;

            var highest = start;
            for (var j = start + 1; j <= i; j++)
            {
                if (value(minutes[j]) > value(minutes[highest]))
                    highest = j;
            }

            var peak = minutes[highest];
            var from = Math.Max(0, highest - CourseMinutes);
            var to = Math.Min(minutes.Count - 1, highest + CourseMinutes);
            yield return new LoadPeak(
                kind,
                minutes[start].Time,
                i - start + 1,
                peak.Time,
                value(peak)!.Value,
                limit,
                Enumerable.Range(start, i - start + 1).Any(j => minutes[j].ThrottlingPercent > 0),
                kind == PeakKind.MemoryPressure ? peak.TopByMemory : peak.TopByCpu,
                [.. Enumerable.Range(from, to - from + 1).Select(j => (minutes[j].Time, value(minutes[j])))]);
        }
    }
}
