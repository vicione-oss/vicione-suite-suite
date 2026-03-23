using System.Globalization;
using Blazor.Shared.SystemInformation.Enums;
using Blazor.Shared.SystemInformation.Extensions;
using Blazor.Shared.SystemInformation.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Client.Services;
using ViciOne.SystemMonitoring.Collectors;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.SystemInformation.Components;

public sealed partial class ProcessComponent : IDisposable
{
    private float _cpuValue;
    private float _ramValue;
    private float _hddValue;
    private float _netValue;
    private float _loadAvg1Value;
    private float _loadAvg5Value;
    private float _loadAvg15Value;
    private bool _loadingSpinnerVisible = true;
    private IEnumerable<LinuxProcessInfo> _processes = [];
    private string _filterText = string.Empty;
    private bool _filterApplied;
    private bool _initialized;

    [Inject]
    public MonitoringService MonitoringService { get; set; } = default!;

    [Inject]
    public ILayoutService LayoutService { get; set; } = default!;

    protected override Task OnInitializedAsync()
    {
        LayoutService.TitleBarAppName = Localization.ProcessComponent.ProcessOverview;

        if (MonitoringService.CurrentProcessList != null)
            OnProcessListChanged(MonitoringService.CurrentProcessList);

        MonitoringService.MetricChanged += OnMetricChanged;
        MonitoringService.ProcessListChanged += OnProcessListChanged;

        return Task.CompletedTask;
    }

    private void OnMetricChanged(MetricType metricType)
    {
        switch (metricType)
        {
            case MetricType.Cpu:
                _cpuValue = (float)Math.Round(MonitoringService.CurrentCpuValue, 1);
                break;
            case MetricType.Ram:
                _ramValue = (float)Math.Round(MonitoringService.CurrentRamValue, 1);
                break;
            case MetricType.Hdd:
                _hddValue = (float)Math.Round(MonitoringService.CurrentHddValue, 1);
                break;
            case MetricType.Net:
                _netValue = (float)Math.Round(MonitoringService.CurrentNetValue, 1);
                break;
            case MetricType.LoadAvg1:
                _loadAvg1Value = (float)Math.Round(MonitoringService.CurrentLoadAvg1Value, 2);
                break;
            case MetricType.LoadAvg5:
                _loadAvg5Value = (float)Math.Round(MonitoringService.CurrentLoadAvg5Value, 2);
                break;
            case MetricType.LoadAvg15:
                _loadAvg15Value = (float)Math.Round(MonitoringService.CurrentLoadAvg15Value, 2);
                break;
            default:
                break;
        }
    }

    private void OnProcessListChanged(IEnumerable<LinuxProcessInfo> list)
    {
        _processes = list;
        _processes = _processes.OrderBy(p => p.Pid);
        _loadingSpinnerVisible = !_processes.Any();

        if (!_initialized)
        {
            _cpuValue = (float)Math.Round(MonitoringService.CurrentCpuValue, 1);
            _ramValue = (float)Math.Round(MonitoringService.CurrentRamValue, 1);
            _hddValue = (float)Math.Round(MonitoringService.CurrentHddValue, 1);
            _netValue = (float)Math.Round(MonitoringService.CurrentNetValue, 1);
            _loadAvg1Value = (float)Math.Round(MonitoringService.CurrentLoadAvg1Value, 2);
            _loadAvg5Value = (float)Math.Round(MonitoringService.CurrentLoadAvg5Value, 2);
            _loadAvg15Value = (float)Math.Round(MonitoringService.CurrentLoadAvg15Value, 2);

            _initialized = true;
        }

        if (_filterApplied)
            ApplyFilter(_filterText);

        InvokeAsync(StateHasChanged);
    }

    private void ApplyFilter(string filter)
    {
        _filterText = filter;
        _filterApplied = false;

        if (!string.IsNullOrWhiteSpace(_filterText))
        {
            _processes = _processes.Where(i =>
                i.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase) ||
                i.CmdLineContains(_filterText) ||
                i.State.Contains(_filterText, StringComparison.OrdinalIgnoreCase));

            _filterApplied = true;
        }
    }

    private static string[] SplitStateInformation(string state)
    {
        var stateInformation = state.Replace("(", string.Empty, StringComparison.Ordinal)
                .Replace(")", string.Empty, StringComparison.Ordinal)
                .Split(" ");

        if (stateInformation.Length == 1 && string.IsNullOrEmpty(stateInformation[0]))
            return [CommonVocabulary.Unknown, CommonVocabulary.Unknown];

        if (stateInformation.Length == 1)
            return [stateInformation[0], CommonVocabulary.Unknown];

        return stateInformation;
    }

    private static string FormatLoadAvgValue(double value, IFormatProvider? provider = null)
        => string.Format(provider is null ? CultureInfo.CurrentUICulture : provider, "{0:F2}", value);

    private static string FormatHardwareValue(double value, IFormatProvider? provider = null)
        => string.Format(provider is null ? CultureInfo.CurrentUICulture : provider, "{0:F1}", value);

    public void Dispose()
    {
        MonitoringService.MetricChanged -= OnMetricChanged;
        MonitoringService.ProcessListChanged -= OnProcessListChanged;
    }
}
