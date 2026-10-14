using System.Globalization;
using Microsoft.AspNetCore.Components;
using ProcessComponentText = Blazor.Shared.SystemInformation.Components.Localization.ProcessComponent;

namespace Blazor.Shared.SystemInformation.Components.ProcessOverview;

/// <summary>
/// "Normal" or the number above the threshold, the same wording in every panel of the process overview.
/// </summary>
public sealed partial class StatusChip
{
    [Parameter, EditorRequired] public int OverThreshold { get; set; }

    [Parameter] public bool ShowDot { get; set; }

    private string StatusName => OverThreshold > 0 ? "warning" : "normal";

    private string Text => OverThreshold > 0
        ? string.Format(CultureInfo.CurrentCulture, ProcessComponentText.OverThreshold, OverThreshold)
        : ProcessComponentText.WithinLimits;
}
