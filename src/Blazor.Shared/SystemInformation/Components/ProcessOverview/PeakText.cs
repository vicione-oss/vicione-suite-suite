using System.Globalization;
using Blazor.Shared.SystemInformation.Extensions;
using Blazor.Shared.SystemInformation.Models;
using ViciOne.Ui.Localization.Resources;
using Text = Blazor.Shared.SystemInformation.Components.Localization.ProcessComponent;

namespace Blazor.Shared.SystemInformation.Components.ProcessOverview;

/// <summary>
/// How peaks and their consumers are named in the list, the chart and the details.
/// </summary>
internal static class PeakText
{
    public static string Name(PeakKind kind)
        => kind switch
        {
            PeakKind.Cpu => TechnicalAcronyms.Cpu,
            PeakKind.Temperature => Text.Temperature,
            _ => Text.PeakMemoryPressure,
        };

    public static string Value(PeakKind kind, double value)
    {
        var number = value.ToString("N0", CultureInfo.CurrentCulture);
        return kind switch
        {
            PeakKind.Temperature => $"{number} °C",
            PeakKind.MemoryPressure => string.Format(CultureInfo.CurrentCulture, Text.PagesPerSecond, number),
            _ => $"{number} %",
        };
    }

    /// <summary>
    /// Engine hosts carry their PID, since several of them run side by side.
    /// </summary>
    public static string Process(TopProcess process)
        => process.Role switch
        {
            SuiteProcessRole.EngineHost => $"{process.Role.DisplayName} ({process.Pid})",
            SuiteProcessRole.Suite => process.Role.DisplayName!,
            _ => process.Name,
        };

    public static string Percent(double value)
        => $"{value.ToString("N0", CultureInfo.CurrentCulture)} %";
}
