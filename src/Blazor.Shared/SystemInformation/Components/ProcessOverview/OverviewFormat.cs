using System.Globalization;
using Text = Blazor.Shared.SystemInformation.Components.Localization.ProcessComponent;

namespace Blazor.Shared.SystemInformation.Components.ProcessOverview;

/// <summary>
/// Number formats shared by the tiles, rows and table of the process overview; a missing value shows as a dash.
/// </summary>
internal static class OverviewFormat
{
    public static string Number(double? value, string format = "N1")
        => value?.ToString(format, CultureInfo.CurrentCulture) ?? "–";

    public static string Percent(double value)
        => string.Format(CultureInfo.CurrentCulture, "{0:N1} %", value);

    public static string Gigabytes(double value)
        => value.ToString("N2", CultureInfo.CurrentCulture);

    /// <summary>
    /// The two largest units only, such as "3 d 04 h" or "12 min", so the column stays narrow.
    /// </summary>
    public static string Duration(TimeSpan? duration)
        => duration switch
        {
            null => "–",
            { TotalDays: >= 1 } d => string.Format(CultureInfo.CurrentCulture, Text.DurationDays, (int)d.TotalDays, d.Hours),
            { TotalHours: >= 1 } d => string.Format(CultureInfo.CurrentCulture, Text.DurationHours, d.Hours, d.Minutes),
            { TotalMinutes: >= 1 } d => string.Format(CultureInfo.CurrentCulture, Text.DurationMinutes, d.Minutes),
            var d => string.Format(CultureInfo.CurrentCulture, Text.DurationSeconds, d.Value.Seconds),
        };

    public static string MegabytesFromKilobytes(long kilobytes)
        => (kilobytes / 1024d).ToString("N1", CultureInfo.CurrentCulture);
}
