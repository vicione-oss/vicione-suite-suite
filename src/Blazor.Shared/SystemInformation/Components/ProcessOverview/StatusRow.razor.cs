using System.Globalization;
using Blazor.Shared.SystemInformation.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazor.Shared.SystemInformation.Components.ProcessOverview;

/// <summary>
/// One line of a side panel: status, name, history and current value, the same for system values and suite processes.
/// </summary>
public sealed partial class StatusRow
{
    [Parameter, EditorRequired] public OverviewStatus Status { get; set; }

    [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;

    [Parameter] public string? Subtitle { get; set; }

    [Parameter, EditorRequired] public MetricHistory History { get; set; } = default!;

    [Parameter, EditorRequired] public double Max { get; set; }

    [Parameter] public double? Limit { get; set; }

    [Parameter, EditorRequired] public string Value { get; set; } = string.Empty;

    [Parameter] public string? Tooltip { get; set; }

    /// <summary>
    /// Colors the value when it differs from <see cref="Status"/>, such as for a collapsed row that shows the status of hidden rows.
    /// </summary>
    [Parameter] public OverviewStatus? ValueStatus { get; set; }

    /// <summary>
    /// A part of the row above it, such as a core under the CPU.
    /// </summary>
    [Parameter] public bool Indented { get; set; }

    /// <summary>
    /// Current share from 0 to 100 as a short bar before the history; no bar when <c>null</c>.
    /// </summary>
    [Parameter] public double? Bar { get; set; }

    /// <summary>
    /// Whether the rows below this one are shown; <c>null</c> for a row without such rows. <see cref="OnClick"/> toggles it.
    /// </summary>
    [Parameter] public bool? Expanded { get; set; }

    /// <summary>
    /// Keeps the row aligned with rows that have an expander.
    /// </summary>
    [Parameter] public bool ReserveExpanderSpace { get; set; }

    [Parameter] public EventCallback OnClick { get; set; }

    /// <summary>
    /// Read out instead of the row content when <see cref="OnClick"/> is set.
    /// </summary>
    [Parameter] public string? ClickLabel { get; set; }

    private static string BarWidth(double share)
        => $"{Math.Clamp(share, 0, 100).ToString("0.#", CultureInfo.InvariantCulture)}%";

    private async Task OnKeyDown(KeyboardEventArgs args)
    {
        if (args.Key is "Enter" or " ")
            await OnClick.InvokeAsync();
    }
}
