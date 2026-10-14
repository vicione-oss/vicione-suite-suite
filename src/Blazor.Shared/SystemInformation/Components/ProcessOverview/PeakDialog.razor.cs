using System.Globalization;
using Blazor.Shared.SystemInformation.Components.Charts;
using Blazor.Shared.SystemInformation.Models;
using Microsoft.AspNetCore.Components;
using Text = Blazor.Shared.SystemInformation.Components.Localization.ProcessComponent;

namespace Blazor.Shared.SystemInformation.Components.ProcessOverview;

/// <summary>
/// A peak in detail: how long it lasted, its course around the highest minute and that minute's largest consumers.
/// </summary>
public sealed partial class PeakDialog
{
    [Parameter] public LoadPeak? Peak { get; set; }

    /// <summary>
    /// The processes running now, to mark consumers that have ended since.
    /// </summary>
    [Parameter] public IReadOnlySet<int> RunningPids { get; set; } = new HashSet<int>();

    [Parameter] public bool Visible { get; set; }

    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }

    [Inject(Key = Sdk.Constants.ClientTimeProviderServiceKey)]
    private TimeProvider TimeProvider { get; set; } = default!;

    private string? Title => Peak is { } peak
        ? string.Format(CultureInfo.CurrentCulture, Text.PeakTitle, PeakText.Name(peak.Kind), PeakText.Value(peak.Kind, peak.PeakValue), LocalTime(peak.PeakTime))
        : null;

    private async Task UpdateVisible(bool value)
    {
        Visible = value;
        await VisibleChanged.InvokeAsync(value);
    }

    private static string Summary(LoadPeak peak)
    {
        var above = string.Format(CultureInfo.CurrentCulture, Text.PeakAbove, peak.Minutes, PeakText.Value(peak.Kind, peak.Limit));
        return peak.Throttled ? $"{above} · {Text.PeakThrottled}" : above;
    }

    /// <summary>
    /// A share runs from 0 to 100; other values in four round steps from zero, or for a temperature from a little below the
    /// lowest value shown.
    /// </summary>
    private static (double Minimum, double Maximum) Scale(LoadPeak peak)
    {
        if (peak.Kind == PeakKind.Cpu)
            return (0, 100);

        var values = peak.Course.Select(point => point.Value).OfType<double>().Append(peak.Limit).ToList();
        var minimum = peak.Kind == PeakKind.Temperature ? Math.Max(0, Math.Floor((values.Min() - 5) / 5) * 5) : 0;
        var step = RoundStep((Math.Max(values.Max() * 1.1, peak.Limit * 1.2) - minimum) / 4);
        return (minimum, minimum + (4 * step));
    }

    private static double RoundStep(double step)
    {
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(Math.Max(step, 0.1))));
        var factor = step / magnitude;
        return magnitude * (factor <= 1 ? 1 : factor <= 2 ? 2 : factor <= 2.5 ? 2.5 : factor <= 5 ? 5 : 10);
    }

    /// <summary>
    /// The minutes the peak stayed above its threshold.
    /// </summary>
    private static ChartRange Window(LoadPeak peak)
        => new(SlotOf(peak, peak.Start), SlotOf(peak, peak.Start + TimeSpan.FromMinutes(peak.Minutes)));

    private static int SlotOf(LoadPeak peak, DateTimeOffset time)
    {
        var first = peak.Course[0].Time;
        var last = peak.Course[^1].Time;
        return last > first ? (int)Math.Clamp(Math.Round((time - first) / (last - first) * (peak.Course.Count - 1)), 0, peak.Course.Count - 1) : 0;
    }

    private static string SeriesClass(PeakKind kind)
        => kind switch
        {
            PeakKind.Cpu => "cpu",
            PeakKind.Temperature => "temperature",
            _ => "memory",
        };

    private string LocalTime(DateTimeOffset time)
        => TimeZoneInfo.ConvertTime(time, TimeProvider.LocalTimeZone).ToString("HH:mm", CultureInfo.InvariantCulture);
}
