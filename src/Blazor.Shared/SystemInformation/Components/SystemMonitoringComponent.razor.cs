using System.Globalization;
using System.Text.Json;
using Blazor.Shared.SystemInformation.Components.Charts;
using Blazor.Shared.SystemInformation.Components.ProcessOverview;
using Blazor.Shared.SystemInformation.Models;
using Blazor.Shared.SystemInformation.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Sdk.Client.Services;
using ViciOne.Ui.Localization.Resources;
using ProcessComponentText = Blazor.Shared.SystemInformation.Components.Localization.ProcessComponent;
using Text = Blazor.Shared.SystemInformation.Components.Localization.SystemMonitoringComponent;

namespace Blazor.Shared.SystemInformation.Components;

/// <summary>
/// CPU, memory, disk and network of the last hour or day as lines, with the values of a moment in the gauges below.
/// </summary>
/// <remarks>
/// The chart raises <c>chart-hover</c>; system-monitoring-component.js shows the values of that slot in the gauges without a round
/// trip to the server.
/// </remarks>
public sealed partial class SystemMonitoringComponent : IAsyncDisposable
{
    private const double GaugeCenter = 30;
    private const double GaugeRadius = 24;

    private static readonly SystemMonitoringSeries[] s_series = Enum.GetValues<SystemMonitoringSeries>();
    private static readonly double[] s_networkTops = [0.5, 1, 2, 5, 10, 20, 50, 100, 200, 500, 1000];

    private ElementReference _root;
    private IJSObjectReference? _module;
    private bool _day;
    private SystemMonitoringSeries? _focus;
    private SystemMonitoringChartData _data = SystemMonitoringChartData.Create([], DateTimeOffset.UtcNow, false);
    private CultureInfo _culture = CultureInfo.CurrentCulture;

    [Inject] private DailyRangeService DailyRanges { get; set; } = default!;

    [Inject] private IJsInterop JsInterop { get; set; } = default!;

    [Inject(Key = Sdk.Constants.ClientTimeProviderServiceKey)]
    private TimeProvider TimeProvider { get; set; } = default!;

    public async ValueTask DisposeAsync()
    {
        DailyRanges.Changed -= OnChanged;

        if (_module is null)
            return;

        try
        {
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone and the module with it.
        }
    }

    protected override void OnInitialized()
    {
        _culture = CultureInfo.CurrentCulture;
        DailyRanges.Changed += OnChanged;
    }

    protected override void OnParametersSet()
        => Refresh();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (!firstRender)
            return;

        _module = await JsInterop.IncludeModuleScript<SharedClientModule>("system-monitoring-component.js");
        if (_module is not null)
            await _module.InvokeVoidAsync("attach", _root);
    }

    private void Refresh()
        => _data = SystemMonitoringChartData.Create(DailyRanges.Samples(), TimeProvider.GetUtcNow(), _day);

    private void OnChanged()
        => _ = InvokeAsync(() =>
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _culture;
            Refresh();
            StateHasChanged();
        });

    private void SetDay(bool day)
    {
        _day = day;
        Refresh();
    }

    private void ToggleFocus(SystemMonitoringSeries series)
        => _focus = _focus == series ? null : series;

    private bool InMbit(SystemMonitoringSeries series)
        => series == SystemMonitoringSeries.Network && _data.NetworkInMbit;

    /// <summary>
    /// Mbit/s have no fixed top, so the line uses the next round value above the highest one of the span.
    /// </summary>
    private double NetworkTop()
    {
        var max = _data.Range(SystemMonitoringSeries.Network)?.Max ?? 0;
        foreach (var top in s_networkTops)
        {
            if (top >= max)
                return top;
        }

        return max;
    }

    /// <summary>
    /// A value on the chart's scale of 0 to 100.
    /// </summary>
    private double Scaled(SystemMonitoringSeries series, double value)
        => InMbit(series) ? value / NetworkTop() * 100 : value;

    private ChartLine[] ChartLines()
        => [.. s_series.Select(series => new ChartLine(
            CssName(series),
            [.. _data.Values[series].Select(v => v is { } value ? Scaled(series, value) : (double?)null)],
            _focus is null ? ChartEmphasis.Normal : _focus == series ? ChartEmphasis.Focus : ChartEmphasis.Dim))];

    /// <summary>
    /// The lowest to the highest value of the focused series with its average.
    /// </summary>
    private ChartLevel? ChartLevel()
        => _focus is { } focus && _data.Range(focus) is { } range
            ? new ChartLevel(CssName(focus), Scaled(focus, range.Min), Scaled(focus, range.Max), Scaled(focus, range.Average))
            : null;

    private string NetworkTopText()
        => $"{NetworkTop().ToString("0.#", CultureInfo.CurrentCulture)} Mbit/s";

    /// <summary>
    /// The values live in memory only, so a gap at the start means the Suite has not been running longer.
    /// </summary>
    private string NoDataText()
        => FirstSlotWithData() is null
            ? ProcessComponentText.NoValuesYet
            : $"{Text.NoDataBeforeStart}\n{string.Format(CultureInfo.CurrentCulture, Text.RunningFor, OverviewFormat.Duration(TimeProvider.GetUtcNow() - DailyRanges.Started))}";

    /// <summary>
    /// The first slot with a value of any series; the slots before it have no data, for example after a restart.
    /// </summary>
    private int? FirstSlotWithData()
    {
        int? first = null;
        foreach (var values in _data.Values.Values)
        {
            var index = Array.FindIndex(values, v => v is not null);
            if (index >= 0 && (first is null || index < first))
                first = index;
        }

        return first;
    }

    private string Number(SystemMonitoringSeries series, double? value)
        => value is not { } v ? "–" : InMbit(series) ? v.ToString("N1", CultureInfo.CurrentCulture) : Math.Round(v).ToString("N0", CultureInfo.CurrentCulture);

    private string Unit(SystemMonitoringSeries series)
        => InMbit(series) ? "Mbit/s" : "%";

    private double Fraction(SystemMonitoringSeries series, double value)
        => Math.Clamp(InMbit(series) ? value / NetworkTop() : value / 100, 0, 1);

    /// <summary>
    /// A part of the gauge ring, which is open at the bottom and runs clockwise over 270°.
    /// </summary>
    /// <param name="from">The start as a share of the ring, 0 to 1.</param>
    /// <param name="to">The end as a share of the ring, 0 to 1.</param>
    private static string Arc(double from, double to)
    {
        static (double X, double Y) Point(double share)
        {
            var angle = Math.PI * (0.75 + 1.5 * share);
            return (GaugeCenter + GaugeRadius * Math.Cos(angle), GaugeCenter + GaugeRadius * Math.Sin(angle));
        }

        var start = Point(from);
        var end = Point(to);
        var large = (to - from) * 270 > 180 ? 1 : 0;
        return $"M{Svg(start.X)} {Svg(start.Y)} A{Svg(GaugeRadius)} {Svg(GaugeRadius)} 0 {large} 1 {Svg(end.X)} {Svg(end.Y)}";
    }

    private bool IsOver(SystemMonitoringSeries series)
        => Limit(series) is { } limit && _data.Latest(series) > limit;

    /// <summary>
    /// The warning threshold of the process overview; none for Mbit/s, which have no fixed top.
    /// </summary>
    private double? Limit(SystemMonitoringSeries series)
        => InMbit(series)
            ? null
            : series switch
            {
                SystemMonitoringSeries.Cpu => ProcessOverviewThresholds.CpuPercent,
                SystemMonitoringSeries.Memory => ProcessOverviewThresholds.MemoryPercent,
                SystemMonitoringSeries.Disk => ProcessOverviewThresholds.DiskPercent,
                _ => ProcessOverviewThresholds.NetworkPercent,
            };

    private string GaugeLabel(SystemMonitoringSeries series)
    {
        var (min, max) = Stats(series);
        return string.Format(CultureInfo.CurrentCulture, Text.GaugeLabel, Name(series), $"{Number(series, _data.Latest(series))} {Unit(series)}", min, max);
    }

    private (string Min, string Max) Stats(SystemMonitoringSeries series)
    {
        if (_data.Range(series) is not { } range)
            return ("–", "–");

        // Rounding the minimum down and the maximum up keeps the current value inside the shown range.
        var factor = InMbit(series) ? 10 : 1;
        var format = InMbit(series) ? "N1" : "N0";
        string Rounded(double value, Func<double, double> round) => (round(Math.Round(value * factor, 6)) / factor).ToString(format, CultureInfo.CurrentCulture);
        return (Rounded(range.Min, Math.Floor), Rounded(range.Max, Math.Ceiling));
    }

    private static string Name(SystemMonitoringSeries series)
        => series switch
        {
            SystemMonitoringSeries.Cpu => TechnicalAcronyms.Cpu,
            SystemMonitoringSeries.Memory => TechnicalTerms.Memory,
            SystemMonitoringSeries.Disk => ProcessComponentText.Disk,
            _ => CommonVocabulary.Network,
        };

    private static string CssName(SystemMonitoringSeries series)
        => series switch
        {
            SystemMonitoringSeries.Cpu => "cpu",
            SystemMonitoringSeries.Memory => "memory",
            SystemMonitoringSeries.Disk => "disk",
            _ => "network",
        };

    /// <summary>
    /// What the script needs to show a slot in the gauges: per series its share of the ring and its formatted value.
    /// </summary>
    private string HoverData()
        => JsonSerializer.Serialize(new
        {
            series = s_series.Select(s => new
            {
                share = _data.Values[s].Select(v => v is { } value ? Math.Round(Fraction(s, value), 4) : (double?)null),
                text = _data.Values[s].Select(v => Number(s, v)),
                limit = Limit(s) is { } limit ? Fraction(s, limit) : (double?)null,
                now = new
                {
                    share = _data.Latest(s) is { } latest ? Math.Round(Fraction(s, latest), 4) : (double?)null,
                    text = Number(s, _data.Latest(s)),
                },
            }),
        });

    private static string Svg(double value)
        => value.ToString("0.#", CultureInfo.InvariantCulture);
}
