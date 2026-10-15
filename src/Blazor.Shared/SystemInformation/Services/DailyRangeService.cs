using Blazor.Shared.SystemInformation.Models;
using Core.Shared.Monitoring;
using Microsoft.Extensions.Options;
using ViciOne.SystemMonitoring;
using ViciOne.SystemMonitoring.Collectors;
using ViciOne.SystemMonitoring.Metrics;

namespace Blazor.Shared.SystemInformation.Services;

/// <summary>
/// Keeps the one-minute values of the system monitoring for a day, for the ranges in the process overview tiles and the
/// chart in the system flyout.
/// </summary>
/// <remarks>
/// Besides the minutes it reads the CPU every few seconds, to keep the lowest and highest reading of each minute.
/// </remarks>
internal sealed class DailyRangeService(IOptions<SystemMonitoringOptions> options, ILiveMonitoring? liveMonitoring = null) : IOutput, IDisposable
{
    /// <summary>
    /// One sample per minute, the interval of the system monitoring.
    /// </summary>
    private const int MinutesPerDay = 24 * 60;

    private static readonly TimeSpan s_cpuInterval = TimeSpan.FromSeconds(5);

    private readonly Lock _lock = new();
    private readonly RingBuffer _cpu = new(MinutesPerDay);
    private readonly RingBuffer _memory = new(MinutesPerDay);
    private readonly RingBuffer _disk = new(MinutesPerDay);
    private readonly RingBuffer _network = new(MinutesPerDay);
    private readonly RingBuffer _networkThroughput = new(MinutesPerDay);
    private readonly RingBuffer _temperature = new(MinutesPerDay);
    private readonly RingBuffer _diskWrite = new(MinutesPerDay);
    private readonly RingBuffer _written = new(MinutesPerDay);
    private readonly RingBuffer _received = new(MinutesPerDay);
    private readonly RingBuffer _sent = new(MinutesPerDay);
    private readonly Queue<SystemMonitoringSample> _samples = new(MinutesPerDay);
    private readonly Queue<MinuteLoad> _minutes = new(MinutesPerDay);

    private string? _networkInterface;
    private IDisposable? _cpuSubscription;
    private double? _minuteCpuMin;
    private double? _minuteCpuMax;

    /// <summary>
    /// When the Suite started collecting; the history starts here.
    /// </summary>
    public DateTimeOffset Started { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The stretches of the day above a warning threshold, newest first.
    /// </summary>
    public IReadOnlyList<LoadPeak> Peaks { get; private set; } = [];

    public DailyRanges Current { get; private set; } = new(null, null, null, null, null, null, null, null, null, null);

    /// <summary>
    /// Raised after a new minute was added, outside the lock.
    /// </summary>
    public event Action? Changed;

    public Task Connect(CancellationToken cancellationToken)
    {
        lock (_lock)
            _cpuSubscription ??= liveMonitoring?.Subscribe(s_cpuInterval, [Collectors.Cpu], OnCpuSample);

        return Task.CompletedTask;
    }

    public Task Disconnect(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _cpuSubscription?.Dispose();
            _cpuSubscription = null;
        }
    }

    public Task Publish(IReadOnlyCollection<Metric> metrics, DateTimeOffset timestamp, CancellationToken cancellationToken)
    {
        var list = metrics as IReadOnlyList<Metric> ?? [.. metrics];

        lock (_lock)
        {
            _networkInterface = MetricSelection.SelectNetworkInterface(list, options.Value.DefaultNetworkInterface, _networkInterface);

            var cpu = MetricSelection.CpuPercent(list);
            _cpu.Add(cpu);
            var memory = MetricSelection.MemoryPercent(list);
            _memory.Add(memory);
            var disk = MetricSelection.DiskPercent(list);
            _disk.Add(disk);
            var network = _networkInterface is null || MetricSelection.NetworkPorts(list, _networkInterface).Count > 0
                ? null
                : MetricSelection.NetworkPercent(list, _networkInterface);
            _network.Add(network);
            var throughput = _networkInterface is null ? null : MetricSelection.Throughput(list, _networkInterface);
            _networkThroughput.Add(throughput);
            _temperature.Add(MetricSelection.SelectTemperature(list).Value);
            _diskWrite.Add(MetricSelection.DiskWriteBytesPerSecond(list));
            _written.Add(MetricSelection.DiskWrittenBytes(list));
            _received.Add(_networkInterface is null ? null : MetricSelection.ReceivedBytes(list, _networkInterface));
            _sent.Add(_networkInterface is null ? null : MetricSelection.SentBytes(list, _networkInterface));

            if (_samples.Count == MinutesPerDay)
                _samples.Dequeue();
            _samples.Enqueue(new(timestamp, cpu, memory, disk, network, throughput));

            if (_minutes.Count == MinutesPerDay)
                _minutes.Dequeue();
            var minute = MinuteLoad(list, timestamp);
            if (_minuteCpuMin is { } low && _minuteCpuMax is { } high)
                minute = minute with { CpuMin = Math.Min(low, minute.Cpu ?? low), CpuMax = Math.Max(high, minute.Cpu ?? high) };
            _minuteCpuMin = null;
            _minuteCpuMax = null;
            _minutes.Enqueue(minute);
            Peaks = LoadPeaks.Find([.. _minutes]);

            Current = new(_cpu.ToRange(), _memory.ToRange(), _disk.ToRange(), _network.ToRange(), _networkThroughput.ToRange(), _temperature.ToRange(),
                _diskWrite.ToRange(), Sum(_written), Sum(_received), Sum(_sent));
        }

        Changed?.Invoke();
        return Task.CompletedTask;
    }

    /// <summary>
    /// The load of each minute of the last day with its largest consumers, oldest first.
    /// </summary>
    public MinuteLoad[] Minutes()
    {
        lock (_lock)
            return [.. _minutes];
    }

    /// <summary>
    /// The minutes of the last day, oldest first.
    /// </summary>
    public SystemMonitoringSample[] Samples()
    {
        lock (_lock)
            return [.. _samples];
    }

    private void OnCpuSample(LiveSample sample)
    {
        if (MetricSelection.CpuPercent(sample.Metrics) is not { } cpu)
            return;

        lock (_lock)
        {
            _minuteCpuMin = Math.Min(_minuteCpuMin ?? cpu, cpu);
            _minuteCpuMax = Math.Max(_minuteCpuMax ?? cpu, cpu);
        }
    }

    private static MinuteLoad MinuteLoad(IReadOnlyList<Metric> metrics, DateTimeOffset timestamp)
    {
        var processes = metrics.FirstOrDefault(m => m is { Group: Collectors.Process, Name: "processes" })?.Value as List<LinuxProcessInfo> ?? [];
        static TopProcess Top(LinuxProcessInfo process)
            => new(process.Pid, process.Name, ProcessRoles.Of(process), process.CpuUsage, process.MemoryDetails.VmRSSKb);

        return new(
            timestamp,
            MetricSelection.CpuPercent(metrics),
            MetricSelection.SelectTemperature(metrics).Value,
            MetricSelection.RefaultsPerSecond(metrics),
            MetricSelection.Throttling(metrics).ThrottlingPercent,
            [.. processes.Where(p => p.CpuUsage > 0).OrderByDescending(p => p.CpuUsage).Take(3).Select(Top)],
            [.. processes.OrderByDescending(p => p.MemoryDetails.VmRSSKb).Take(3).Select(Top)]);
    }

    private static double? Sum(RingBuffer buffer)
    {
        var values = buffer.ToArray();
        return values.Length == 0 ? null : values.Sum();
    }
}
