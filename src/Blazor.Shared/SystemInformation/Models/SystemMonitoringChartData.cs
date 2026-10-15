namespace Blazor.Shared.SystemInformation.Models;

public enum SystemMonitoringSeries
{
    Cpu,
    Memory,
    Disk,
    Network,
}

/// <summary>
/// The samples of one time span placed in fixed slots, so a short history starts at the right edge and gaps stay gaps.
/// </summary>
/// <param name="SlotEnds">End of each slot, oldest first.</param>
/// <param name="Values">Average of the samples in each slot; <c>null</c> for a slot without samples.</param>
public sealed record SystemMonitoringChartData(
    IReadOnlyList<DateTimeOffset> SlotEnds,
    IReadOnlyDictionary<SystemMonitoringSeries, double?[]> Values)
{
    private static readonly TimeSpan s_diskInterval = TimeSpan.FromMinutes(5);

    public int SlotCount => SlotEnds.Count;

    /// <summary>
    /// The network values are Mbit/s instead of a share, because the interface reports no link speed.
    /// </summary>
    public bool NetworkInMbit { get; init; }

    /// <summary>
    /// One hour in 60 one-minute slots, or a day in 96 slots of 15 minutes.
    /// </summary>
    public static SystemMonitoringChartData Create(IReadOnlyList<SystemMonitoringSample> samples, DateTimeOffset now, bool day)
    {
        var (slots, slotLength) = day ? (96, TimeSpan.FromMinutes(15)) : (60, TimeSpan.FromMinutes(1));
        // Slots end on whole minutes or quarter hours, so their times read like a clock.
        var end = new DateTimeOffset((now.UtcTicks + slotLength.Ticks - 1) / slotLength.Ticks * slotLength.Ticks, TimeSpan.Zero);
        var start = end - slotLength * slots;
        var slotEnds = Enumerable.Range(1, slots).Select(i => start + slotLength * i).ToArray();

        var networkInMbit = !samples.Any(s => s.Network is not null) && samples.Any(s => s.NetworkMbit is not null);
        var sums = Enum.GetValues<SystemMonitoringSeries>().ToDictionary(s => s, _ => new (double Sum, int Count)[slots]);
        double? diskBefore = null;
        foreach (var sample in samples)
        {
            if (sample.Time <= start && sample.Time > start - s_diskInterval && sample.Disk is { } disk)
                diskBefore = disk;

            if (sample.Time <= start || sample.Time > end)
                continue;

            var slot = Math.Min(slots - 1, (int)((sample.Time - start).Ticks / slotLength.Ticks));
            foreach (var (series, slotSums) in sums)
            {
                if (ValueOf(sample, series, networkInMbit) is { } value)
                    slotSums[slot] = (slotSums[slot].Sum + value, slotSums[slot].Count + 1);
            }
        }

        var values = sums.ToDictionary(s => s.Key, s => s.Value.Select(x => x.Count == 0 ? (double?)null : x.Sum / x.Count).ToArray());
        CarryForward(values[SystemMonitoringSeries.Disk], diskBefore, (int)Math.Ceiling(s_diskInterval / slotLength));
        return new(slotEnds, values) { NetworkInMbit = networkInMbit };
    }

    /// <summary>
    /// Minimum, average and maximum of the present values; <c>null</c> without any.
    /// </summary>
    public ValueRange? Range(SystemMonitoringSeries series)
    {
        var present = Values[series].OfType<double>().ToArray();
        return present.Length == 0 ? null : new(present.Min(), present.Average(), present.Max());
    }

    public double? Latest(SystemMonitoringSeries series)
        => Values[series].LastOrDefault(v => v is not null);

    private static double? ValueOf(SystemMonitoringSample sample, SystemMonitoringSeries series, bool networkInMbit)
        => series switch
        {
            SystemMonitoringSeries.Cpu => sample.Cpu,
            SystemMonitoringSeries.Memory => sample.Memory,
            SystemMonitoringSeries.Disk => sample.Disk,
            _ => networkInMbit ? sample.NetworkMbit : sample.Network,
        };

    /// <summary>
    /// The disk is measured every 5 minutes; until the next reading the last one holds, also one from just before the span,
    /// so the line has no gaps. A longer gap stays one.
    /// </summary>
    private static void CarryForward(double?[] values, double? before, int maxSlots)
    {
        var last = before;
        var age = 0;
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] is { } value)
            {
                last = value;
                age = 0;
            }
            else if (last is not null && ++age <= maxSlots)
            {
                values[i] = last;
            }
        }
    }
}
