using System.Globalization;
using System.Text.Json;
using Blazor.Shared.SystemInformation.Components.Charts;
using Blazor.Shared.SystemInformation.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Sdk.Client.Services;
using Text = Blazor.Shared.SystemInformation.Components.Localization.ProcessComponent;

namespace Blazor.Shared.SystemInformation.Components.ProcessOverview;

/// <summary>
/// The CPU since the start, up to the last 24 hours, with the lowest and highest reading of each minute and the peaks, and
/// the largest consumers of now or of the minute under the pointer.
/// </summary>
/// <remarks>
/// The chart raises <c>chart-hover</c>; peak-chart.js shows the consumers and the total of that slot in elements of its
/// own, marked <c>data-hover</c>, in place of those of now, which stay Blazor's.
/// </remarks>
public sealed partial class PeakChart : IAsyncDisposable
{
    private static readonly TimeSpan s_shortestSpan = TimeSpan.FromHours(1);
    private static readonly TimeSpan s_longestSpan = TimeSpan.FromHours(24);

    private ElementReference _root;
    private bool _disposed;
    private IJSObjectReference? _module;
    private (DateTimeOffset? Last, int Count, int Peaks) _rendered;
    private bool _hasChanges = true;
    private DateTimeOffset _start;
    private DateTimeOffset _end;
    private int _slotCount = 2;
    private ChartLine[] _lines = [];
    private ChartBand[] _bands = [];
    private ChartMarker[] _markers = [];
    private LoadPeak[] _markerPeaks = [];
    private int _noDataSlots;
    private string _noDataText = string.Empty;
    private IReadOnlyList<TopProcess> _nowTop = [];
    private string _nowTotal = "–";
    private string _slotData = "[]";

    [Parameter, EditorRequired] public IReadOnlyList<MinuteLoad> Minutes { get; set; } = [];

    [Parameter, EditorRequired] public IReadOnlyList<LoadPeak> Peaks { get; set; } = [];

    [Parameter] public EventCallback<LoadPeak> OnPeakSelected { get; set; }

    /// <summary>
    /// When the Suite started collecting; the chart spans the time since then, from one hour up to a day.
    /// </summary>
    [Parameter] public DateTimeOffset Started { get; set; }

    [Inject] private IJsInterop JsInterop { get; set; } = default!;

    [Inject(Key = Sdk.Constants.ClientTimeProviderServiceKey)]
    private TimeProvider TimeProvider { get; set; } = default!;

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
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

    /// <summary>
    /// The time since the start, at least an hour and at most a day.
    /// </summary>
    internal static TimeSpan Span(DateTimeOffset started, DateTimeOffset now)
    {
        var running = now - started;
        return running < s_shortestSpan ? s_shortestSpan : running > s_longestSpan ? s_longestSpan : running;
    }

    protected override void OnParametersSet()
    {
        var state = (Minutes.Count > 0 ? Minutes[^1].Time : (DateTimeOffset?)null, Minutes.Count, Peaks.Count);
        _hasChanges = state != _rendered;
        if (!_hasChanges)
            return;

        _rendered = state;
        Build();
    }

    protected override bool ShouldRender()
        => _hasChanges;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (!firstRender)
            return;

        _module = await JsInterop.IncludeModuleScript<SharedClientModule>("peak-chart.js");

        // Disposed while the script loaded; the element is gone.
        if (_disposed)
        {
            await DisposeAsync();
            return;
        }

        if (_module is not null)
            await _module.InvokeVoidAsync("attach", _root);
    }

    private Task SelectMarker(int index)
        => OnPeakSelected.InvokeAsync(_markerPeaks[index]);

    private void Build()
    {
        _end = TimeProvider.GetUtcNow();
        var span = Span(Started, _end);
        _start = _end - span;

        // Beyond six hours the busiest minute of every five keeps short peaks visible at this width.
        var slot = span > TimeSpan.FromHours(6) ? TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(1);
        _slotCount = Math.Max(2, (int)Math.Ceiling(span / slot) + 1);
        var slots = new MinuteLoad?[_slotCount];
        var lows = new double?[_slotCount];
        var highs = new double?[_slotCount];
        foreach (var minute in Minutes)
        {
            if (minute.Time < _start || minute.Cpu is null)
                continue;

            var index = SlotOf(minute.Time, span);
            if (slots[index] is not { } busiest || minute.Cpu > busiest.Cpu)
                slots[index] = minute;
            if (minute.CpuMin is { } low)
                lows[index] = Math.Min(lows[index] ?? low, low);
            if (minute.CpuMax is { } high)
                highs[index] = Math.Max(highs[index] ?? high, high);
        }

        _lines = [new ChartLine("cpu", [.. slots.Select(minute => minute?.Cpu)])];
        _bands = [new ChartBand("cpu", lows, highs)];

        _markerPeaks = [.. Peaks.Where(peak => peak.Kind == PeakKind.Cpu && peak.PeakTime >= _start)];
        _markers = [.. _markerPeaks.Select(peak => new ChartMarker(SlotOf(peak.PeakTime, span), peak.PeakValue, "peak",
            string.Format(CultureInfo.CurrentCulture, Text.PeakDetails, $"{PeakText.Value(peak.Kind, peak.PeakValue)} · {LocalTime(peak.PeakTime)}")))];

        var first = Array.FindIndex(slots, minute => minute is not null);
        _noDataSlots = first < 0 ? _slotCount : first;
        _noDataText = first < 0
            ? Text.NoValuesYet
            : string.Format(CultureInfo.CurrentCulture, Text.NoDataSinceStart, OverviewFormat.Duration(_end - Started));

        var latest = Minutes.Count > 0 ? Minutes[^1] : null;
        _nowTop = latest?.TopByCpu ?? [];
        _nowTotal = latest?.Cpu is { } cpu ? PeakText.Percent(cpu) : "–";
        _slotData = SlotData(slots);
    }

    private int SlotOf(DateTimeOffset time, TimeSpan span)
        => Math.Clamp((int)Math.Round((time - _start) / span * (_slotCount - 1)), 0, _slotCount - 1);

    /// <summary>
    /// Per slot the consumers the script shows under the pointer, already formatted; <c>null</c> for slots without a reading.
    /// </summary>
    private static string SlotData(MinuteLoad?[] slots)
        => JsonSerializer.Serialize(slots.Select(minute => minute is null
            ? null
            : new
            {
                cpu = minute.Cpu is { } cpu ? PeakText.Percent(cpu) : "–",
                top = minute.TopByCpu.Select(process => new
                {
                    n = PeakText.Process(process),
                    v = PeakText.Percent(process.CpuPercent),
                    w = Math.Round(BarWidth(process.CpuPercent), 1),
                    o = process.CpuPercent > ProcessOverviewThresholds.ProcessCpuPercent,
                }),
            }));

    private string LocalTime(DateTimeOffset time)
        => TimeZoneInfo.ConvertTime(time, TimeProvider.LocalTimeZone).ToString("HH:mm", CultureInfo.InvariantCulture);

    private static double BarWidth(double cpuPercent)
        => Math.Clamp(cpuPercent / ProcessOverviewThresholds.ProcessChartMax * 100, 0, 100);

    private static string Css(double value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);
}
