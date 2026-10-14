using Blazor.Shared.SystemInformation.Models;
using Core.Shared.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ViciOne.SystemMonitoring;
using ViciOne.SystemMonitoring.Collectors;
using ViciOne.SystemMonitoring.Metrics;

namespace Blazor.Shared.SystemInformation.Services;

/// <summary>
/// Keeps the live system monitoring at two seconds while at least one process overview is open and builds its snapshots.
/// </summary>
internal sealed class ProcessOverviewSampler(IServiceProvider services, IOptions<SystemMonitoringOptions> options) : IDisposable
{
    /// <summary>
    /// Two minutes at <see cref="Interval"/>.
    /// </summary>
    public const int HistoryLength = 60;

    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Registered only on Linux with the system monitoring enabled.
    /// </summary>
    private readonly ILiveMonitoring? _liveMonitoring = services.GetService<ILiveMonitoring>();

    private readonly Lock _lock = new();
    private readonly Dictionary<int, RingBuffer> _processHistories = [];

    private readonly RingBuffer _cpu = new(HistoryLength);
    private readonly RingBuffer _memory = new(HistoryLength);
    private readonly RingBuffer _disk = new(HistoryLength);
    private readonly RingBuffer _network = new(HistoryLength);
    private readonly RingBuffer _networkThroughput = new(HistoryLength);
    private readonly RingBuffer _loadAverage = new(HistoryLength);
    private readonly RingBuffer _temperature = new(HistoryLength);
    private readonly RingBuffer _networkReceived = new(HistoryLength);
    private readonly RingBuffer _networkSent = new(HistoryLength);
    private readonly RingBuffer _networkErrors = new(HistoryLength);
    private readonly RingBuffer _networkDropped = new(HistoryLength);
    private readonly RingBuffer _swap = new(HistoryLength);
    private readonly RingBuffer _swapPages = new(HistoryLength);
    private readonly RingBuffer _diskWrite = new(HistoryLength);
    private readonly RingBuffer _secondTemperature = new(HistoryLength);
    private readonly RingBuffer _ioWait = new(HistoryLength);
    private readonly RingBuffer _refaults = new(HistoryLength);
    private readonly List<RingBuffer> _cores = [];

    private int _subscribers;
    private string? _networkInterface;
    private IDisposable? _liveSubscription;

    public bool IsAvailable => _liveMonitoring is not null;

    public ProcessOverviewSnapshot? Current { get; private set; }

    /// <summary>
    /// Raised on the sampling thread after <see cref="Current"/> changed.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Keeps the sampling running until the returned handle is disposed.
    /// </summary>
    public IDisposable Subscribe()
    {
        lock (_lock)
        {
            if (++_subscribers == 1 && _liveMonitoring is not null)
            {
                ResetHistories();
                _liveSubscription = _liveMonitoring.Subscribe(Interval, OnSampled);
            }
        }

        return new Subscription(this);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _subscribers = 0;
            Stop();
        }
    }

    private void Unsubscribe()
    {
        lock (_lock)
        {
            if (--_subscribers == 0)
                Stop();
        }
    }

    private void Stop()
    {
        _liveSubscription?.Dispose();
        _liveSubscription = null;
    }

    private void ResetHistories()
    {
        _processHistories.Clear();
        _networkInterface = null;
        foreach (var history in new[] { _cpu, _memory, _disk, _network, _networkThroughput, _networkReceived, _networkSent, _networkErrors, _networkDropped, _loadAverage, _temperature, _swap, _swapPages, _diskWrite, _secondTemperature, _ioWait, _refaults })
            history.Clear();
        _cores.Clear();
        Current = null;
    }

    private void OnSampled(LiveSample sample)
    {
        lock (_lock)
        {
            if (_subscribers == 0)
                return;

            Current = Apply(sample.Metrics, sample.Timestamp);
        }

        Changed?.Invoke();
    }

    private ProcessOverviewSnapshot Apply(IReadOnlyList<Metric> metrics, DateTimeOffset timestamp)
    {
        _networkInterface = MetricSelection.SelectNetworkInterface(metrics, options.Value.DefaultNetworkInterface, _networkInterface);
        var network = _networkInterface;
        IReadOnlyList<NetworkPort> ports = network is null ? [] : MetricSelection.NetworkPorts(metrics, network);
        var linkSpeed = network is null || ports.Count > 0 ? null : MetricSelection.Find(metrics, Collectors.Network, "speed", network);
        var (temperatureSensor, temperature) = MetricSelection.SelectTemperature(metrics);

        _cpu.Add(MetricSelection.CpuPercent(metrics));
        _memory.Add(MetricSelection.MemoryPercent(metrics));
        _disk.Add(MetricSelection.DiskPercent(metrics));
        _loadAverage.Add(MetricSelection.Find(metrics, Collectors.LoadAverage, "load1"));
        _network.Add(network is null || ports.Count > 0 ? null : MetricSelection.NetworkPercent(metrics, network));
        _networkThroughput.Add(network is null ? null : MetricSelection.Throughput(metrics, network));
        _networkReceived.Add(network is null ? null : MetricSelection.ReceivedMbit(metrics, network));
        _networkSent.Add(network is null ? null : MetricSelection.SentMbit(metrics, network));
        _networkErrors.Add(network is null ? null : MetricSelection.NetworkErrors(metrics, network));
        _networkDropped.Add(network is null ? null : MetricSelection.NetworkDropped(metrics, network));

        var swapTotal = MetricSelection.SwapTotalKb(metrics);
        var swapUsed = MetricSelection.SwapUsedKb(metrics);
        _swap.Add(swapTotal > 0 ? swapUsed / swapTotal * 100 : null);
        _swapPages.Add(MetricSelection.SwapPages(metrics));
        _diskWrite.Add(MetricSelection.DiskWriteBytesPerSecond(metrics));
        _ioWait.Add(MetricSelection.IoWaitPercent(metrics));
        _refaults.Add(MetricSelection.RefaultsPerSecond(metrics));
        var (throttleFrom, throttling) = MetricSelection.Throttling(metrics);

        var (secondSensor, secondTemperature) = MetricSelection.SelectSecondTemperature(metrics, temperatureSensor);
        _secondTemperature.Add(secondTemperature);
        _temperature.Add(temperature);

        var cores = MetricSelection.CorePercents(metrics);
        while (_cores.Count < cores.Count)
            _cores.Add(new(HistoryLength));
        for (var i = 0; i < cores.Count; i++)
            _cores[i].Add(cores[i]);

        var system = new SystemMetrics(
            _cpu.ToHistory(),
            _memory.ToHistory(),
            _disk.ToHistory(),
            _network.ToHistory(),
            _loadAverage.ToHistory(),
            _temperature.ToHistory(),
            network,
            linkSpeed,
            _networkThroughput.ToHistory(),
            network is null ? null : MetricSelection.ReceivedMbit(metrics, network),
            network is null ? null : MetricSelection.SentMbit(metrics, network),
            MetricSelection.Find(metrics, Collectors.LoadAverage, "load5"),
            MetricSelection.Find(metrics, Collectors.LoadAverage, "load15"),
            MetricSelection.Find(metrics, Collectors.Uptime, "uptime_seconds"),
            MetricSelection.DiskFreeGb(metrics),
            MetricSelection.MemoryFreePercent(metrics),
            temperatureSensor,
            [.. _cores.Select(c => c.ToHistory())],
            _networkReceived.ToHistory(),
            _networkSent.ToHistory(),
            _networkErrors.ToHistory(),
            _networkDropped.ToHistory(),
            _swap.ToHistory(),
            _swapPages.ToHistory(),
            swapTotal,
            swapUsed,
            _diskWrite.ToHistory(),
            _secondTemperature.ToHistory(),
            secondSensor,
            ports,
            MetricSelection.CpuClock(metrics),
            _ioWait.ToHistory(),
            _refaults.ToHistory(),
            throttleFrom,
            throttling);

        return new(timestamp, system, ApplyProcesses(metrics));
    }

    private List<ProcessEntry> ApplyProcesses(IReadOnlyList<Metric> metrics)
    {
        var processes = metrics.FirstOrDefault(m => m is { Group: Collectors.Process, Name: "processes" })?.Value as List<LinuxProcessInfo> ?? [];
        var entries = new List<ProcessEntry>(processes.Count);

        foreach (var process in processes)
        {
            if (!_processHistories.TryGetValue(process.Pid, out var history))
                _processHistories[process.Pid] = history = new(HistoryLength);

            history.Add(process.CpuUsage);

            entries.Add(new(process, history.ToHistory(), ProcessRoles.Of(process)));
        }

        var running = processes.Select(p => p.Pid).ToHashSet();
        foreach (var pid in _processHistories.Keys.Where(pid => !running.Contains(pid)).ToList())
            _processHistories.Remove(pid);

        return entries;
    }

    private sealed class Subscription(ProcessOverviewSampler sampler) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                sampler.Unsubscribe();
        }
    }
}
