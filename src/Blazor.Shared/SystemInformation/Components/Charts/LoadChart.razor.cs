using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Sdk.Client.Services;

namespace Blazor.Shared.SystemInformation.Components.Charts;

/// <summary>
/// Values of the system over time: lines, bands, a threshold, a highlighted range, the time before any data and markers.
/// </summary>
/// <remarks>
/// Only the lines are SVG, stretched to the width; labels, markers and the cursor are HTML on top, so text keeps its shape at
/// any width. Hovering runs in the browser (load-chart.js) and raises a <c>chart-hover</c> event with the slot under the pointer,
/// or <c>null</c> once it leaves, for the surrounding component to show the values of that slot.
/// </remarks>
public sealed partial class LoadChart : IAsyncDisposable
{
    private static readonly int[] s_gridValues = [0, 25, 50, 75, 100];

    private ElementReference _root;
    private bool _disposed;
    private IJSObjectReference? _module;
    private (string CssClass, string Points)[] _lines = [];
    private (string CssClass, string Points)[] _bands = [];
    private (double Percent, string Label)[] _ticks = [];
    private string _hoverData = "{}";

    /// <summary>
    /// The time of the first slot; the slots are spread evenly up to <see cref="End"/>.
    /// </summary>
    [Parameter, EditorRequired] public DateTimeOffset Start { get; set; }

    /// <summary>
    /// The time of the last slot, shown as "now".
    /// </summary>
    [Parameter, EditorRequired] public DateTimeOffset End { get; set; }

    [Parameter, EditorRequired] public int Slots { get; set; }

    /// <summary>
    /// The value at the bottom edge; values outside the scale are cut off at its edges.
    /// </summary>
    [Parameter] public double Minimum { get; set; }

    [Parameter] public double Maximum { get; set; } = 100;

    /// <summary>
    /// The label at the left end of the time axis; none when not set.
    /// </summary>
    [Parameter] public string? StartLabel { get; set; }

    /// <summary>
    /// The label at the right end of the time axis; "now" when not set.
    /// </summary>
    [Parameter] public string? EndLabel { get; set; }

    [Parameter] public ChartRange? Highlight { get; set; }

    [Parameter] public IReadOnlyList<ChartLine> Lines { get; set; } = [];

    [Parameter] public IReadOnlyList<ChartBand> Bands { get; set; } = [];

    [Parameter] public ChartLevel? Level { get; set; }

    /// <summary>
    /// A dashed line across the width, on the chart's scale.
    /// </summary>
    [Parameter] public double? Threshold { get; set; }

    /// <summary>
    /// Leading slots without data, hatched.
    /// </summary>
    [Parameter] public int NoDataSlots { get; set; }

    /// <summary>
    /// Shown in the hatched area when it is wide enough; a line break starts a second line.
    /// </summary>
    [Parameter] public string? NoDataText { get; set; }

    /// <summary>
    /// A note at the top right, such as the top of a scale that is not a share.
    /// </summary>
    [Parameter] public string? TopLabel { get; set; }

    [Parameter] public IReadOnlyList<ChartMarker> Markers { get; set; } = [];

    /// <summary>
    /// Called with the index of the selected marker in <see cref="Markers"/>.
    /// </summary>
    [Parameter] public EventCallback<int> OnMarkerSelected { get; set; }

    [Parameter] public int PlotHeight { get; set; } = 84;

    [Parameter] public string? AriaLabel { get; set; }

    [Parameter] public string? CssClass { get; set; }

    [Inject] private IJsInterop JsInterop { get; set; } = default!;

    [Inject(Key = Sdk.Constants.ClientTimeProviderServiceKey)]
    private TimeProvider TimeProvider { get; set; } = default!;

    private double NoDataPercent => SlotPercent(Math.Min(NoDataSlots, Slots - 1));

    private string YLabelFormat => Maximum - Minimum < 10 ? "0.#" : "N0";

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

    protected override void OnParametersSet()
    {
        _lines = [.. Lines.SelectMany(line => Runs(line.Values).Select(points => (LineClass(line), points)))];
        _bands = [.. Bands.SelectMany(band => Areas(band.Low, band.High).Select(points => (band.CssClass, points)))];
        _ticks = [.. Ticks()];
        _hoverData = HoverData();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        _module = await JsInterop.IncludeModuleScript<SharedClientModule>("load-chart.js");

        // Disposed while the script loaded; the element is gone.
        if (_disposed)
        {
            await DisposeAsync();
            return;
        }

        if (_module is not null)
            await _module.InvokeVoidAsync("attach", _root);
    }

    private static string LineClass(ChartLine line)
        => line.Emphasis switch
        {
            ChartEmphasis.Focus => $"{line.CssClass} focus",
            ChartEmphasis.Dim => $"{line.CssClass} dim",
            _ => line.CssClass,
        };

    /// <summary>
    /// The distance from the top edge in percent of the height.
    /// </summary>
    private double Top(double value)
        => Maximum > Minimum ? 100 - (Math.Clamp((value - Minimum) / (Maximum - Minimum), 0, 1) * 100) : 100;

    private double GridValue(int percent)
        => Minimum + ((Maximum - Minimum) * percent / 100);

    private double SlotPercent(int slot)
        => Slots > 1 ? Math.Clamp(slot / (double)(Slots - 1), 0, 1) * 100 : 0;

    /// <summary>
    /// One polyline per run of present values, so a gap stays visible; a single value becomes a dot.
    /// </summary>
    private IEnumerable<string> Runs(IReadOnlyList<double?> values)
    {
        var run = new StringBuilder();
        var count = 0;
        for (var i = 0; i <= values.Count; i++)
        {
            if (i < values.Count && values[i] is { } value)
            {
                run.Append(count == 0 ? null : " ").Append(Svg(SlotPercent(i) * 10)).Append(',').Append(Svg(Top(value)));
                count++;
                continue;
            }

            if (count > 0)
                yield return count == 1 ? $"{run} {run}" : run.ToString();
            run.Clear();
            count = 0;
        }
    }

    private IEnumerable<string> Areas(IReadOnlyList<double?> lows, IReadOnlyList<double?> highs)
    {
        var upper = new List<string>();
        var lower = new List<string>();
        for (var i = 0; i <= lows.Count; i++)
        {
            if (i < lows.Count && i < highs.Count && lows[i] is { } low && highs[i] is { } high)
            {
                var x = Svg(SlotPercent(i) * 10);
                upper.Add($"{x},{Svg(Top(high))}");
                lower.Insert(0, $"{x},{Svg(Top(low))}");
                continue;
            }

            if (upper.Count > 1)
                yield return string.Join(' ', upper.Concat(lower));
            upper.Clear();
            lower.Clear();
        }
    }

    /// <summary>
    /// Round local times, every 5 minutes up to half an hour and up to every six hours for a day; left out where they would
    /// touch an edge.
    /// </summary>
    private IEnumerable<(double Percent, string Label)> Ticks()
    {
        var span = End - Start;
        if (span <= TimeSpan.Zero)
            yield break;

        var step = span <= TimeSpan.FromMinutes(30) ? TimeSpan.FromMinutes(5)
            : span <= TimeSpan.FromHours(2) ? TimeSpan.FromMinutes(15)
            : span <= TimeSpan.FromHours(6) ? TimeSpan.FromHours(1)
            : span <= TimeSpan.FromHours(12) ? TimeSpan.FromHours(3)
            : TimeSpan.FromHours(6);
        var local = TimeZoneInfo.ConvertTime(Start, TimeProvider.LocalTimeZone);
        var tick = new DateTimeOffset((local.DateTime.Ticks + step.Ticks - 1) / step.Ticks * step.Ticks, local.Offset);
        for (; tick < End; tick += step)
        {
            var percent = (tick - Start) / span * 100;
            if (percent is > 4 and < 86 && (StartLabel is null || percent > 14))
                yield return (percent, tick.ToString("HH:mm", CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// What the script needs for hovering: the local time of each slot and the height of each line in it.
    /// </summary>
    private string HoverData()
    {
        var times = Enumerable.Range(0, Math.Max(Slots, 0))
            .Select(i => Slots > 1 ? Start + ((End - Start) * (i / (double)(Slots - 1))) : End)
            .Select(time => TimeZoneInfo.ConvertTime(time, TimeProvider.LocalTimeZone).ToString("HH:mm", CultureInfo.InvariantCulture));
        return JsonSerializer.Serialize(new
        {
            t = times,
            y = Lines.Select(line => line.Values.Select(v => v is { } value ? Math.Round(Top(value), 2) : (double?)null)),
        });
    }

    private static string Svg(double value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);
}
