using System.Globalization;
using Blazor.Shared.SystemInformation.Extensions;
using Blazor.Shared.SystemInformation.Models;
using Microsoft.AspNetCore.Components;
using static Blazor.Shared.SystemInformation.Components.ProcessOverview.OverviewFormat;
using Text = Blazor.Shared.SystemInformation.Components.Localization.ProcessComponent;

namespace Blazor.Shared.SystemInformation.Components.ProcessOverview;

/// <summary>
/// CPU or memory of the device split by process group; a group in the legend filters the process table.
/// </summary>
/// <remarks>
/// The CPU bar fills only up to the load, so its legend adds up to the CPU of the device and never to 100 %. The memory bar
/// spans the whole memory, with cache and free memory after the groups.
/// </remarks>
public sealed partial class ProcessGroupBar
{
    private static readonly (ProcessGroup Group, string CssClass)[] s_groups =
    [
        (ProcessGroup.Suite, "suite"),
        (ProcessGroup.System, "system"),
        (ProcessGroup.Kernel, "kernel"),
        (ProcessGroup.Other, "other"),
    ];

    private List<Part> _parts = [];
    private double _rest;
    private double _total = 1;
    private bool _byMemory;
    private bool _sharedCounted;

    [Parameter, EditorRequired] public IReadOnlyList<ProcessEntry> Processes { get; set; } = [];

    /// <summary>
    /// CPU of the whole device in percent.
    /// </summary>
    [Parameter] public double? CpuPercent { get; set; }

    /// <summary>
    /// Memory that is not available in percent of the total.
    /// </summary>
    [Parameter] public double? MemoryPercent { get; set; }

    [Parameter] public double? MemoryFreePercent { get; set; }

    [Parameter] public double TotalMemoryKb { get; set; }

    [Parameter] public ProcessGroup? Selected { get; set; }

    [Parameter] public EventCallback<ProcessGroup?> SelectedChanged { get; set; }

    protected override void OnParametersSet()
        => BuildParts();

    private void BuildParts()
    {
        var parts = new List<Part>();
        var assigned = 0d;

        foreach (var (group, cssClass) in s_groups)
        {
            var value = Processes
                .Where(p => p.Group == group)
                .Sum(p => _byMemory ? p.Info.MemoryDetails.VmRSSKb : p.CpuUsage);
            assigned += value;
            parts.Add(new(group, group.DisplayName, cssClass, value, Format(value), value));
        }

        if (_byMemory && TotalMemoryKb > 0)
        {
            var used = (MemoryPercent ?? 0) / 100 * TotalMemoryKb;
            var free = (MemoryFreePercent ?? 0) / 100 * TotalMemoryKb;
            var cache = Math.Max(TotalMemoryKb - used - free, 0);

            // RES counts shared memory once per process, so the groups can exceed the used memory.
            _sharedCounted = assigned > used;
            if (_sharedCounted && assigned > 0)
                parts = [.. parts.Select(p => p with { Width = p.Value * used / assigned })];

            AddInfo(parts, Text.Unassigned, "unassigned", Math.Max(used - assigned, 0));
            AddInfo(parts, Text.CacheLabel, "cache", cache);
            AddInfo(parts, Text.FreeLabel, "free", free);
            _total = TotalMemoryKb;
        }
        else if (!_byMemory)
        {
            _sharedCounted = false;
            AddInfo(parts, Text.Unassigned, "unassigned", Math.Max((CpuPercent ?? assigned) - assigned, 0));
            _total = 100;
        }

        _parts = parts;
        _rest = Math.Max(_total - parts.Sum(p => p.Width), 0);
    }

    private void ShowMemory(bool byMemory)
    {
        _byMemory = byMemory;
        BuildParts();
    }

    private void AddInfo(List<Part> parts, string label, string cssClass, double value)
        => parts.Add(new(null, label, cssClass, value, Format(value), value));

    private string Format(double value)
    {
        if (!_byMemory)
            return Percent(value);

        var megabytes = value / 1024;
        return megabytes >= 1024 ? $"{Gigabytes(megabytes / 1024)} GB" : $"{Number(megabytes, "N0")} MB";
    }

    private string Share(double value)
        => Math.Max(value / _total, 0.002).ToString("0.####", CultureInfo.InvariantCulture);

    private async Task Select(ProcessGroup group)
        => await SelectedChanged.InvokeAsync(Selected == group ? null : group);

    /// <summary>
    /// <c>Group</c> is <c>null</c> for parts that only inform, such as free memory; <c>Width</c> is the share drawn in the bar.
    /// </summary>
    private sealed record Part(ProcessGroup? Group, string Label, string CssClass, double Value, string Text, double Width);
}
