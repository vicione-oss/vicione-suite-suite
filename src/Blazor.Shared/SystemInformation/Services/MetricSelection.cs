using System.Globalization;
using Blazor.Shared.SystemInformation.Models;
using Core.Shared.Monitoring;
using ViciOne.SystemMonitoring;
using ViciOne.SystemMonitoring.Metrics;

namespace Blazor.Shared.SystemInformation.Services;

/// <summary>
/// Picks the values of the process overview out of one collection of system monitoring metrics.
/// </summary>
internal static class MetricSelection
{
    /// <summary>
    /// The disk collector names the root drive with a backslash.
    /// </summary>
    private const string RootDrive = "\\";

    /// <summary>
    /// Loopback and the bridges and virtual links of containers and VMs.
    /// </summary>
    private static readonly string[] s_virtualInterfacePrefixes = ["lo", "docker", "veth", "br-", "virbr"];

    public static double? CpuPercent(IReadOnlyList<Metric> metrics)
        => Find(metrics, Collectors.Cpu, "total_system", "total");

    /// <summary>
    /// Busy share of each core, ordered by core number; the CPU collector reports cores as <c>core0</c>, <c>core1</c>, ….
    /// </summary>
    public static List<double> CorePercents(IReadOnlyList<Metric> metrics)
        => [.. metrics
            .Where(m => m is { Group: Collectors.Cpu, SubGroup: { } core, Name: "user" or "system" or "steal" } && core.StartsWith("core", StringComparison.Ordinal))
            .GroupBy(m => int.TryParse(m.SubGroup.AsSpan(4), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : -1)
            .Where(g => g.Key >= 0)
            .OrderBy(g => g.Key)
            .Select(g => g.Sum(m => ToDouble(m.Value) ?? 0))];

    /// <summary>
    /// <c>null</c> without cpufreq, such as in a virtual machine.
    /// </summary>
    public static CpuClock? CpuClock(IReadOnlyList<Metric> metrics)
    {
        if (Find(metrics, Collectors.CpuFrequency, "current", "total") is not { } current
            || Find(metrics, Collectors.CpuFrequency, "maximum", "total") is not { } maximum)
            return null;

        var cores = metrics
            .Where(m => m is { Group: Collectors.CpuFrequency, SubGroup: { } core } && core.StartsWith("core", StringComparison.Ordinal))
            .Select(m => m.SubGroup!)
            .Distinct()
            .Select(core => (Name: core, Number: int.TryParse(core.AsSpan(4), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : -1))
            .Where(core => core.Number >= 0)
            .OrderBy(core => core.Number)
            .Select(core => (
                Current: Find(metrics, Collectors.CpuFrequency, "current", core.Name) ?? 0,
                Limit: Find(metrics, Collectors.CpuFrequency, "limit", core.Name),
                Maximum: Find(metrics, Collectors.CpuFrequency, "maximum", core.Name)))
            .ToList();
        var limited = cores
            .Where(core => core.Limit < core.Maximum)
            .OrderBy(core => core.Limit / core.Maximum)
            .Select(core => core.Limit)
            .FirstOrDefault();

        return new(current, maximum, limited, [.. cores.Select(core => core.Current)]);
    }

    /// <summary>
    /// Share that is not available, so cache that is given back on demand does not count.
    /// </summary>
    public static double? MemoryPercent(IReadOnlyList<Metric> metrics)
        => Find(metrics, Collectors.Ram, "total_memory_usage");

    public static double? MemoryFreePercent(IReadOnlyList<Metric> metrics)
        => 100 - Find(metrics, Collectors.Ram, "used_memory_usage");

    public static double? DiskPercent(IReadOnlyList<Metric> metrics)
        => Find(metrics, Collectors.Disk, "usage", RootDrive);

    public static double? DiskFreeGb(IReadOnlyList<Metric> metrics)
        => Find(metrics, Collectors.Disk, "totalFreeSpaceGB", RootDrive);

    public static double? NetworkPercent(IReadOnlyList<Metric> metrics, string networkInterface)
        => Find(metrics, Collectors.Network, "total_bandwidth_usage", networkInterface);

    /// <summary>
    /// The sockets of a built-in switch whose traffic runs over <paramref name="networkInterface"/>, such as lan1 and lan2 behind end0.
    /// </summary>
    public static IReadOnlyList<NetworkPort> NetworkPorts(IReadOnlyList<Metric> metrics, string networkInterface)
        => [.. metrics
            .Where(m => m.Group == Collectors.Network && m.Name == "conduit" && m.SubGroup is not null && Equals(m.Value, networkInterface))
            .Select(m => m.SubGroup!)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .Select(port => Find(metrics, Collectors.Network, "carrier", port) == 1
                ? new NetworkPort(port, true, Find(metrics, Collectors.Network, "speed", port))
                : new NetworkPort(port, false, null))];

    /// <summary>
    /// The first match is the current value; aggregated 5 and 15 minute values of the same name follow it.
    /// </summary>
    public static double? Find(IReadOnlyList<Metric> metrics, string group, string name, string? subGroup = null)
    {
        var metric = metrics.FirstOrDefault(m => m.Group == group && m.Name == name && (subGroup is null || m.SubGroup == subGroup));
        return metric is null ? null : ToDouble(metric.Value);
    }

    /// <summary>
    /// The configured interface when it carries traffic, otherwise <paramref name="previous"/> while it exists, so the history
    /// does not mix interfaces; a new choice prefers the default route over the busiest interface.
    /// </summary>
    public static string? SelectNetworkInterface(IReadOnlyList<Metric> metrics, string? configured, string? previous)
    {
        var interfaces = metrics
            .Where(m => m.Group == Collectors.Network && m.SubGroup is { } name && !IsVirtualInterface(name))
            .Select(m => m.SubGroup!)
            .Distinct()
            .ToList();

        if (configured is not null && interfaces.Contains(configured) && Throughput(metrics, configured) > 0)
            return configured;

        if (previous is not null && interfaces.Contains(previous))
            return previous;

        if (DefaultRouteInterface() is { } defaultRoute && interfaces.Contains(defaultRoute))
            return defaultRoute;

        return interfaces.OrderByDescending(i => Throughput(metrics, i) ?? 0).FirstOrDefault();
    }

    /// <summary>
    /// Faulty packets in both directions and carrier errors since the previous reading.
    /// </summary>
    public static double? NetworkErrors(IReadOnlyList<Metric> metrics, string networkInterface)
        => Sum(metrics, networkInterface, "rx_errors", "tx_errors", "carrier_errors");

    /// <summary>
    /// Packets the kernel discarded since the previous reading; on Wi-Fi this includes unwanted protocols, so it is no fault.
    /// </summary>
    public static double? NetworkDropped(IReadOnlyList<Metric> metrics, string networkInterface)
        => Sum(metrics, networkInterface, "rx_dropped", "tx_dropped");

    private static double? Sum(IReadOnlyList<Metric> metrics, string networkInterface, params string[] names)
    {
        var counts = names
            .Select(name => Find(metrics, Collectors.Network, name, networkInterface))
            .OfType<double>()
            .ToList();
        return counts.Count == 0 ? null : counts.Sum();
    }

    /// <summary>
    /// Bytes written per second to all physical disks together.
    /// </summary>
    public static double? DiskWriteBytesPerSecond(IReadOnlyList<Metric> metrics)
        => SumOverSubGroups(metrics, Collectors.DiskIo, "write_bytes_per_second");

    /// <summary>
    /// Bytes written to all physical disks together during the interval.
    /// </summary>
    public static double? DiskWrittenBytes(IReadOnlyList<Metric> metrics)
        => SumOverSubGroups(metrics, Collectors.DiskIo, "written_bytes");

    /// <summary>
    /// The hottest sensor other than <paramref name="primarySensor"/>, such as the board next to the CPU.
    /// </summary>
    public static (string? Sensor, double? Value) SelectSecondTemperature(IReadOnlyList<Metric> metrics, string? primarySensor)
    {
        var sensors = metrics
            .Where(m => m is { Group: Collectors.ThermalSensors, Unit: MetricUnit.DegreeCelsius } && m.Name != primarySensor)
            .Select(m => (Sensor: (string?)m.Name, Value: ToDouble(m.Value)))
            .Where(s => s.Value > 0)
            .ToList();
        return sensors.Count == 0 ? (null, null) : sensors.MaxBy(s => s.Value);
    }

    /// <summary>
    /// Aggregated values are skipped, as only plain numbers convert.
    /// </summary>
    private static double? SumOverSubGroups(IReadOnlyList<Metric> metrics, string group, string name)
    {
        var values = metrics
            .Where(m => m.Group == group && m.Name == name)
            .Select(m => ToDouble(m.Value))
            .OfType<double>()
            .ToList();
        return values.Count == 0 ? null : values.Sum();
    }

    public static double? SwapTotalKb(IReadOnlyList<Metric> metrics)
        => Find(metrics, Collectors.Swap, "total");

    public static double? SwapUsedKb(IReadOnlyList<Metric> metrics)
        => Find(metrics, Collectors.Swap, "used");

    public static double? IoWaitPercent(IReadOnlyList<Metric> metrics)
        => Find(metrics, Collectors.Cpu, "iowait", "total");

    public static double? RefaultsPerSecond(IReadOnlyList<Metric> metrics)
        => Find(metrics, Collectors.Ram, "page_cache_refaults_per_second");

    /// <summary>
    /// Where the kernel starts to throttle, from the CPU's thermal zone or else the first zone with a passive trip point, and
    /// the strongest throttling of all zones; both <c>null</c> where no zone reports them.
    /// </summary>
    public static (double? ThrottleFromCelsius, double? ThrottlingPercent) Throttling(IReadOnlyList<Metric> metrics)
    {
        var zones = metrics
            .Where(m => m is { Group: Collectors.ThermalZones, SubGroup: not null })
            .Select(m => m.SubGroup!)
            .Distinct()
            .OrderBy(zone => zone.Contains("cpu", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(zone => zone, StringComparer.Ordinal)
            .ToList();
        var passiveTrip = zones.Select(zone => Find(metrics, Collectors.ThermalZones, "passive_trip", zone)).FirstOrDefault(trip => trip is not null);
        var throttling = zones.Select(zone => Find(metrics, Collectors.ThermalZones, "throttling", zone)).Max();
        return (passiveTrip, throttling);
    }

    /// <summary>
    /// Pages swapped in and out since the previous reading.
    /// </summary>
    public static double? SwapPages(IReadOnlyList<Metric> metrics)
        => Find(metrics, Collectors.Swap, "swap_in") + Find(metrics, Collectors.Swap, "swap_out");

    /// <summary>
    /// Received and sent together in Mbit/s.
    /// </summary>
    public static double? Throughput(IReadOnlyList<Metric> metrics, string networkInterface)
    {
        var received = ReceivedMbit(metrics, networkInterface);
        var sent = SentMbit(metrics, networkInterface);
        return received is null && sent is null ? null : (received ?? 0) + (sent ?? 0);
    }

    /// <summary>
    /// Bytes received during the interval.
    /// </summary>
    public static double? ReceivedBytes(IReadOnlyList<Metric> metrics, string networkInterface)
        => Find(metrics, Collectors.Network, "rx_bytes", networkInterface);

    /// <summary>
    /// Bytes sent during the interval.
    /// </summary>
    public static double? SentBytes(IReadOnlyList<Metric> metrics, string networkInterface)
        => Find(metrics, Collectors.Network, "tx_bytes", networkInterface);

    public static double? ReceivedMbit(IReadOnlyList<Metric> metrics, string networkInterface)
        => Find(metrics, Collectors.Network, "rx_bandwidth", networkInterface) * 8 / 1_000_000;

    public static double? SentMbit(IReadOnlyList<Metric> metrics, string networkInterface)
        => Find(metrics, Collectors.Network, "tx_bandwidth", networkInterface) * 8 / 1_000_000;

    /// <summary>
    /// A sensor with "cpu" in its name, otherwise the hottest; unreadable sensors report -1 and are skipped.
    /// </summary>
    public static (string? Sensor, double? Value) SelectTemperature(IReadOnlyList<Metric> metrics)
    {
        var sensors = metrics
            .Where(m => m is { Group: Collectors.ThermalSensors, Unit: MetricUnit.DegreeCelsius })
            .Select(m => (m.Name, Value: ToDouble(m.Value)))
            .Where(s => s.Value > 0)
            .ToList();

        if (sensors.Count == 0)
            return (null, null);

        var sensor = sensors.FirstOrDefault(s => s.Name.Contains("cpu", StringComparison.OrdinalIgnoreCase));
        if (sensor.Name is null)
            sensor = sensors.MaxBy(s => s.Value);

        return sensor;
    }

    private static bool IsVirtualInterface(string name)
        => s_virtualInterfacePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>
    /// The interface of the default route in <c>/proc/net/route</c>, whose destination is <c>00000000</c>.
    /// </summary>
    private static string? DefaultRouteInterface()
    {
        try
        {
            return File.ReadLines("/proc/net/route")
                .Skip(1)
                .Select(line => line.Split('\t', StringSplitOptions.TrimEntries))
                .FirstOrDefault(fields => fields.Length > 1 && fields[1] == "00000000")?[0];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static double? ToDouble(object value)
        => value is IConvertible convertible and not string ? convertible.ToDouble(CultureInfo.InvariantCulture) : null;
}
