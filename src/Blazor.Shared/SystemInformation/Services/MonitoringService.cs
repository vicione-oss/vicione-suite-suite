using Blazor.Shared.SystemInformation.Enums;
using Core.Shared.Monitoring;
using Microsoft.Extensions.Options;
using ViciOne.SystemMonitoring;
using ViciOne.SystemMonitoring.Collectors;
using ViciOne.SystemMonitoring.Metrics;

namespace Blazor.Shared.SystemInformation.Services;

public class MonitoringService(IOptions<SystemMonitoringOptions> options) : IOutput
{
    internal const int ValuesPerDay = (288 + 1) * 3;
    private const int BufferSize = 5;

    private readonly Func<Metric, bool> _filterCpu = m => m.Group == Collectors.Cpu && m.Name == "total_system" && m.SubGroup == "total";
    private readonly Func<Metric, bool> _filterRam = m => m.Group == Collectors.Ram && m.Name == "total_memory_usage";
    private readonly Func<Metric, bool> _filterNet = m => m.Group == Collectors.Network && m.Name == "total_bandwidth_usage" && m.SubGroup == options.Value.DefaultNetworkInterface;
    private readonly Func<Metric, bool> _filterHdd = m => m.Group == Collectors.Disk && m.Name == "usage" && m.SubGroup == "\\";
    private readonly Func<Metric, bool> _filterProcessList = m => m.Group == Collectors.Process && m.Name == "processes";
    private readonly Func<Metric, bool> _filterLoadAvg1 = m => m.Group == Collectors.LoadAverage && m.Name == "load1";
    private readonly Func<Metric, bool> _filterLoadAvg5 = m => m.Group == Collectors.LoadAverage && m.Name == "load5";
    private readonly Func<Metric, bool> _filterLoadAvg15 = m => m.Group == Collectors.LoadAverage && m.Name == "load15";

    private readonly Lock _lock = new();
    private bool _firstRun = true;

    private readonly List<float> _cpuTempValues = [];
    private readonly List<float> _ramTempValues = [];
    private readonly List<float> _netTempValues = [];

    private readonly List<float> _cpuGraphValues = [];
    private readonly List<float> _ramGraphValues = [];
    private readonly List<float> _hddGraphValues = [];
    private readonly List<float> _netGraphValues = [];

    private List<LinuxProcessInfo> _currentProcessList = [];
    private float _currentCpuValue;
    private float _currentRamValue;
    private float _currentHddValue;
    private float _currentNetValue;
    private float _currentLoadAvg1Value;
    private float _currentLoadAvg5Value;
    private float _currentLoadAvg15Value;

    public IReadOnlyCollection<LinuxProcessInfo> CurrentProcessList => Lock(_currentProcessList.ToArray);
    public float CurrentCpuValue => Lock(() => _currentCpuValue);
    public float CurrentRamValue => Lock(() => _currentRamValue);
    public float CurrentHddValue => Lock(() => _currentHddValue);
    public float CurrentNetValue => Lock(() => _currentNetValue);
    public float CurrentLoadAvg1Value => Lock(() => _currentLoadAvg1Value);
    public float CurrentLoadAvg5Value => Lock(() => _currentLoadAvg5Value);
    public float CurrentLoadAvg15Value => Lock(() => _currentLoadAvg15Value);

    public IReadOnlyCollection<float> CpuGraphValues => Lock(_cpuGraphValues.ToArray);
    public IReadOnlyCollection<float> RamGraphValues => Lock(_ramGraphValues.ToArray);
    public IReadOnlyCollection<float> HddGraphValues => Lock(_hddGraphValues.ToArray);
    public IReadOnlyCollection<float> NetGraphValues => Lock(_netGraphValues.ToArray);

    internal event Func<Task>? GraphValuesChanged;
    internal event Action<MetricType>? MetricChanged;
    internal event Action<IEnumerable<LinuxProcessInfo>>? ProcessListChanged;

    public Task Connect(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task Disconnect(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public async Task Publish(IReadOnlyCollection<Metric> metrics, DateTimeOffset timestamp, CancellationToken cancellationToken)
    {
        var cpuChanged = false;
        var ramChanged = false;
        var hddChanged = false;
        var netChanged = false;
        var loadAvg1Changed = false;
        var loadAvg5Changed = false;
        var loadAvg15Changed = false;
        var processListChanged = false;
        var graphValuesHasChanged = false;

        lock (_lock)
        {
            processListChanged = CollectMetric<List<LinuxProcessInfo>, List<LinuxProcessInfo>>(metrics, _filterProcessList, x => x, null, ref _currentProcessList);
            cpuChanged = CollectMetric<double, float>(metrics, _filterCpu, Convert.ToSingle, _cpuTempValues, ref _currentCpuValue);
            ramChanged = CollectMetric<double, float>(metrics, _filterRam, Convert.ToSingle, _ramTempValues, ref _currentRamValue);
            netChanged = CollectMetric<double, float>(metrics, _filterNet, Convert.ToSingle, _netTempValues, ref _currentNetValue);
            hddChanged = CollectMetric<double, float>(metrics, _filterHdd, Convert.ToSingle, null, ref _currentHddValue);
            loadAvg1Changed = CollectMetric<double, float>(metrics, _filterLoadAvg1, Convert.ToSingle, null, ref _currentLoadAvg1Value);
            loadAvg5Changed = CollectMetric<double, float>(metrics, _filterLoadAvg5, Convert.ToSingle, null, ref _currentLoadAvg5Value);
            loadAvg15Changed = CollectMetric<double, float>(metrics, _filterLoadAvg15, Convert.ToSingle, null, ref _currentLoadAvg15Value);

            graphValuesHasChanged |= UpdateValuesBuffer(_cpuTempValues, _cpuGraphValues);
            graphValuesHasChanged |= UpdateValuesBuffer(_ramTempValues, _ramGraphValues);
            graphValuesHasChanged |= UpdateValuesBuffer(_netTempValues, _netGraphValues);

            if (graphValuesHasChanged)
                UpdateValuesBufferFake(_currentHddValue, _hddGraphValues);

            if (_firstRun)
                _firstRun = false;
        }

        if (cpuChanged)
            MetricChanged?.Invoke(MetricType.Cpu);
        if (ramChanged)
            MetricChanged?.Invoke(MetricType.Ram);
        if (hddChanged)
            MetricChanged?.Invoke(MetricType.Hdd);
        if (netChanged)
            MetricChanged?.Invoke(MetricType.Net);
        if (loadAvg1Changed)
            MetricChanged?.Invoke(MetricType.LoadAvg1);
        if (loadAvg5Changed)
            MetricChanged?.Invoke(MetricType.LoadAvg5);
        if (loadAvg15Changed)
            MetricChanged?.Invoke(MetricType.LoadAvg15);
        if (processListChanged)
            ProcessListChanged?.Invoke(CurrentProcessList);
        if (graphValuesHasChanged && GraphValuesChanged is not null)
            await GraphValuesChanged.Invoke();
    }

    private bool CollectMetric<TRaw, TTarget>(IEnumerable<Metric> metrics, Func<Metric, bool> condition, Func<TRaw, TTarget> convert, List<TTarget>? buffer, ref TTarget current)
    {
        var value = metrics.FirstOrDefault(condition);

        if (value is not null)
        {
            var convertedValue = convert((TRaw)value.Value);
            current = convertedValue;

            if (_firstRun)
            {
                for (var i = 0; i < BufferSize; i++)
                    buffer?.Add(convertedValue);
            }
            else
            {
                buffer?.Add(convertedValue);
            }

            return true;
        }

        return false;
    }

    internal static bool UpdateValuesBuffer(List<float> tempBuffer, List<float> values)
    {
        if (tempBuffer.Count >= BufferSize)
        {
            if (values.Count >= ValuesPerDay)
                values.RemoveRange(ValuesPerDay - 3, 3);

            values.InsertRange(0, tempBuffer.Min(), tempBuffer.Average(), tempBuffer.Max());
            tempBuffer.Clear();

            return true;
        }

        return false;
    }

    private static void UpdateValuesBufferFake(float current, List<float> values)
    {
        if (values.Count >= ValuesPerDay)
            values.RemoveRange(ValuesPerDay - 3, 3);

        var min = 95 * current / 100;
        var max = 105 * current / 100;

        values.InsertRange(0, min, current, max > 100 ? 100 : max);
    }

    private T Lock<T>(Func<T> get)
    {
        lock (_lock)
        {
            return get();
        }
    }
}
