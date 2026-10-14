using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.SystemInformation.Components.ProcessOverview;

/// <summary>
/// The history of a value as a line; the part above <see cref="Limit"/> is highlighted.
/// </summary>
/// <remarks>
/// The samples fill the chart from the right, so a history that is still filling up is not stretched over the whole width.
/// </remarks>
public sealed partial class Sparkline
{
    private const int Width = 120;
    private const int Height = 28;

    private readonly string _overLimitClipId = $"sparkline-over-{Guid.NewGuid():N}";

    private string _points = string.Empty;
    private double? _limitY;
    private bool _hasOverLimit;
    private IReadOnlyList<double> _renderedSamples = [];
    private (int Capacity, double Max, double? Limit) _renderedScale;
    private bool _hasChanges = true;

    [Parameter, EditorRequired] public IReadOnlyList<double> Samples { get; set; } = [];

    /// <summary>
    /// Number of samples the full width stands for.
    /// </summary>
    [Parameter, EditorRequired] public int Capacity { get; set; }

    /// <summary>
    /// Value at the top edge; higher samples are cut off there.
    /// </summary>
    [Parameter, EditorRequired] public double Max { get; set; }

    /// <summary>
    /// <c>null</c> draws no limit line.
    /// </summary>
    [Parameter] public double? Limit { get; set; }

    protected override void OnParametersSet()
    {
        var scaleParameters = (Capacity, Max, Limit);
        _hasChanges = scaleParameters != _renderedScale || !Samples.SequenceEqual(_renderedSamples);
        if (!_hasChanges)
            return;

        _renderedSamples = Samples;
        _renderedScale = scaleParameters;

        var scale = Max > 0 ? (Height - 2) / Max : 0;
        _points = Points(Samples, Math.Max(Capacity, Samples.Count), Max, scale);
        _limitY = Limit is { } limit && limit <= Max ? Height - (limit * scale) : null;
        _hasOverLimit = _limitY is not null && Samples.Any(s => s > Limit);
    }

    protected override bool ShouldRender()
        => _hasChanges;

    private static string Points(IReadOnlyList<double> samples, int capacity, double max, double scale)
    {
        if (samples.Count == 0)
            return string.Empty;

        var step = (double)Width / capacity;
        var offset = capacity - samples.Count;
        var points = new StringBuilder();

        for (var i = 0; i < samples.Count; i++)
        {
            var x = ((offset + i) * step) + (step / 2);
            var y = Height - 1 - (Math.Clamp(samples[i], 0, max) * scale);
            points.Append(i == 0 ? string.Empty : " ").Append(Svg(x)).Append(',').Append(Svg(y));
        }

        // A single point needs a second one to be drawn.
        if (samples.Count == 1)
        {
            var point = points.ToString();
            points.Append(' ').Append(point);
        }

        return points.ToString();
    }

    /// <summary>
    /// SVG coordinates need a dot as decimal separator whatever the UI culture.
    /// </summary>
    private static string Svg(double value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);
}
