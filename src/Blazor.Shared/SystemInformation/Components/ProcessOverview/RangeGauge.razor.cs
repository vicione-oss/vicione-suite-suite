using System.Globalization;
using Blazor.Shared.SystemInformation.Models;
using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.SystemInformation.Components.ProcessOverview;

/// <summary>
/// The current value on a scale as a line, with the band of the day's range and the threshold.
/// </summary>
public sealed partial class RangeGauge
{
    [Parameter, EditorRequired] public double Current { get; set; }

    [Parameter] public double LowerBound { get; set; }

    [Parameter] public double UpperBound { get; set; } = 100;

    /// <summary>
    /// <c>null</c> draws no band, such as before the first minute of monitoring.
    /// </summary>
    [Parameter] public ValueRange? Range { get; set; }

    [Parameter] public double? Limit { get; set; }

    /// <summary>
    /// Further values drawn as faint ticks, such as the 5 and 15 minute load average.
    /// </summary>
    [Parameter] public IReadOnlyList<double> Marks { get; set; } = [];

    [Parameter] public bool Warning { get; set; }

    private string Position(double value)
        => Percent(Share(value));

    private string Width(double from, double to)
        => Percent(Share(to) - Share(from));

    private double Share(double value)
        => UpperBound > LowerBound ? Math.Clamp((value - LowerBound) / (UpperBound - LowerBound), 0, 1) : 0;

    private static string Percent(double share)
        => (share * 100).ToString("0.##", CultureInfo.InvariantCulture) + "%";
}
