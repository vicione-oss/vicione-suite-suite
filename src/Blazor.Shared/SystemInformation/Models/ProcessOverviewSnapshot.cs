using ViciOne.SystemMonitoring.Collectors;

namespace Blazor.Shared.SystemInformation.Models;

/// <summary>
/// One sample of the process overview with the history that led to it.
/// </summary>
public sealed record ProcessOverviewSnapshot(
    DateTimeOffset Timestamp,
    SystemMetrics System,
    IReadOnlyList<ProcessEntry> Processes);

/// <summary>
/// Samples are oldest first; an empty history means the collector delivered nothing yet.
/// </summary>
public sealed record MetricHistory(IReadOnlyList<double> Samples, int Capacity)
{
    public double? Latest => Samples.Count > 0 ? Samples[^1] : null;
}

/// <param name="Cpu">Used share of the CPU in percent.</param>
/// <param name="Memory">Share of the memory that is not available, in percent.</param>
/// <param name="Disk">Used share of the root drive in percent.</param>
/// <param name="Network">Share of the link speed; empty when the interface reports no speed, as Wi-Fi does, or has <see cref="NetworkPorts"/>.</param>
/// <param name="LoadAverage">The 1-minute load average.</param>
/// <param name="Temperature">Empty when the device has no readable temperature sensor.</param>
/// <param name="NetworkInterface">The interface the network values belong to; <c>null</c> without one.</param>
/// <param name="NetworkSpeedMbit">The link speed; <c>null</c> when the interface reports none or has <see cref="NetworkPorts"/>.</param>
/// <param name="NetworkThroughput">Received and sent together in Mbit/s, for interfaces without a link speed.</param>
/// <param name="NetworkReceivedMbit">The latest received rate in Mbit/s.</param>
/// <param name="NetworkSentMbit">The latest sent rate in Mbit/s.</param>
/// <param name="LoadAverage5">The 5-minute load average.</param>
/// <param name="LoadAverage15">The 15-minute load average.</param>
/// <param name="UptimeSeconds">Time since boot in seconds.</param>
/// <param name="DiskFreeGb">Free space on the root drive in GB.</param>
/// <param name="MemoryFreePercent">Neither used nor cache; <see cref="Memory"/> is the share that is not available.</param>
/// <param name="TemperatureSensor">The name of the sensor behind <see cref="Temperature"/>.</param>
/// <param name="CoreCpu">One history per core, in the order of the cores.</param>
/// <param name="NetworkReceived">Received rate in Mbit/s per sample.</param>
/// <param name="NetworkSent">Sent rate in Mbit/s per sample.</param>
/// <param name="NetworkErrors">Faulty packets and carrier errors per sample.</param>
/// <param name="NetworkDropped">Discarded packets per sample.</param>
/// <param name="Swap">Used share of the swap space; empty without swap.</param>
/// <param name="SwapPages">Pages swapped in and out per sample.</param>
/// <param name="SwapTotalKb">Size of the swap space in KiB.</param>
/// <param name="SwapUsedKb">Used swap space in KiB.</param>
/// <param name="DiskWrite">Bytes written per second to all physical disks.</param>
/// <param name="SecondTemperature">A second sensor besides <see cref="Temperature"/>, such as the board; empty without one.</param>
/// <param name="SecondTemperatureSensor">The name of the sensor behind <see cref="SecondTemperature"/>.</param>
/// <param name="NetworkPorts">The sockets of a built-in switch behind <see cref="NetworkInterface"/>; empty without one.</param>
/// <param name="Clock"><c>null</c> without cpufreq.</param>
/// <param name="IoWait">Share of the CPU time spent waiting for the disk.</param>
/// <param name="Refaults">Evicted page cache pages read back from disk per second.</param>
/// <param name="ThrottleFromCelsius">Where the kernel starts to throttle; <c>null</c> without a thermal zone that reports it.</param>
/// <param name="ThrottlingPercent">How strongly the kernel throttles now; <c>null</c> where no cooling device is bound, as on a Raspberry Pi.</param>
public sealed record SystemMetrics(
    MetricHistory Cpu,
    MetricHistory Memory,
    MetricHistory Disk,
    MetricHistory Network,
    MetricHistory LoadAverage,
    MetricHistory Temperature,
    string? NetworkInterface,
    double? NetworkSpeedMbit,
    MetricHistory NetworkThroughput,
    double? NetworkReceivedMbit,
    double? NetworkSentMbit,
    double? LoadAverage5,
    double? LoadAverage15,
    double? UptimeSeconds,
    double? DiskFreeGb,
    double? MemoryFreePercent,
    string? TemperatureSensor,
    IReadOnlyList<MetricHistory> CoreCpu,
    MetricHistory NetworkReceived,
    MetricHistory NetworkSent,
    MetricHistory NetworkErrors,
    MetricHistory NetworkDropped,
    MetricHistory Swap,
    MetricHistory SwapPages,
    double? SwapTotalKb,
    double? SwapUsedKb,
    MetricHistory DiskWrite,
    MetricHistory SecondTemperature,
    string? SecondTemperatureSensor,
    IReadOnlyList<NetworkPort> NetworkPorts,
    CpuClock? Clock,
    MetricHistory IoWait,
    MetricHistory Refaults,
    double? ThrottleFromCelsius,
    double? ThrottlingPercent);

public sealed record ProcessEntry(LinuxProcessInfo Info, MetricHistory Cpu, SuiteProcessRole Role)
{
    /// <summary>
    /// PID of <c>kthreadd</c>, the parent of all kernel threads.
    /// </summary>
    public const int KernelThreadParentPid = 2;

    /// <summary>
    /// Kernel threads have no command line.
    /// </summary>
    public bool IsKernelThread => Info.Pid == KernelThreadParentPid || Info.PPid == KernelThreadParentPid;

    public double CpuUsage => Info.CpuUsage;

    /// <summary>
    /// Services started by <c>init</c> (PID 1) count as system, everything they start in turn as other.
    /// </summary>
    public ProcessGroup Group => Role != SuiteProcessRole.None
        ? ProcessGroup.Suite
        : IsKernelThread
            ? ProcessGroup.Kernel
            : Info.Pid == 1 || Info.PPid == 1 ? ProcessGroup.System : ProcessGroup.Other;

    /// <summary>
    /// How long the process has been running, to the second; <c>null</c> when the start is unknown.
    /// </summary>
    public TimeSpan? RunningFor => Info.StartTime is { } start
        ? TimeSpan.FromSeconds(Math.Max(0, Math.Round((DateTimeOffset.UtcNow - start).TotalSeconds)))
        : null;

    /// <summary>
    /// The state letter of <c>/proc/[pid]/status</c>, such as <c>S</c> of <c>S (sleeping)</c>.
    /// </summary>
    public char StateCode => string.IsNullOrEmpty(Info.State) ? '?' : Info.State[0];

    public OverviewStatus Status => StateCode switch
    {
        'Z' => OverviewStatus.Failed,
        'D' => OverviewStatus.Warning,
        _ when CpuUsage > ProcessOverviewThresholds.ProcessCpuPercent => OverviewStatus.Warning,
        _ => OverviewStatus.Normal,
    };
}
