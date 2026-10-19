using System.Globalization;
using Blazor.Shared.SystemInformation.Extensions;
using Blazor.Shared.SystemInformation.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ViciOne.Ui.Localization.Resources;
using static Blazor.Shared.SystemInformation.Components.ProcessOverview.OverviewFormat;
using Text = Blazor.Shared.SystemInformation.Components.Localization.ProcessComponent;

namespace Blazor.Shared.SystemInformation.Components.ProcessOverview;

/// <summary>
/// The processes of the local system as a sortable list or as a tree of parent and child processes.
/// </summary>
/// <remarks>
/// Rows are sorted by the value as displayed and keep their previous order on a tie, so values changing in a hidden digit
/// do not reorder the table. While the pointer or the focus is in the table or details are open the order holds still.
/// </remarks>
public sealed partial class ProcessTable
{
    private readonly HashSet<int> _openPids = [];
    private readonly HashSet<int> _collapsedPids = [ProcessEntry.KernelThreadParentPid];
    private readonly Dictionary<int, ElementReference> _rowReferences = [];
    private readonly Dictionary<int, int> _rank = [];
    private readonly Dictionary<int, ProcessEntry> _byPid = [];
    private readonly Dictionary<int, int> _childCounts = [];

    private List<Row> _rows = [];
    private string? _filter;
    private bool _showKernelThreads;
    private bool _tree;
    private bool _pointerInTable;
    private bool _focusInTable;
    private int _overThresholdCount;
    private int _kernelThreadCount;
    private ProcessGroup? _groupFilter;
    private SortKey _sortKey = SortKey.Name;
    private bool _sortDescending;
    private int? _pidToFocus;

    private enum SortKey
    {
        Status,
        Pid,
        Name,
        RunningFor,
        Threads,
        Memory,
        Cpu,
    }

    [Parameter, EditorRequired] public IReadOnlyList<ProcessEntry> Processes { get; set; } = [];

    [Parameter, EditorRequired] public double TotalMemoryKb { get; set; }

    /// <summary>
    /// CPU of the whole device in percent.
    /// </summary>
    [Parameter] public double? CpuPercent { get; set; }

    /// <summary>
    /// Memory that is not available in percent of the total.
    /// </summary>
    [Parameter] public double? MemoryPercent { get; set; }

    [Parameter] public double? MemoryFreePercent { get; set; }

    private Column[] Columns =>
    [
        new(SortKey.Status, string.Empty, CommonVocabulary.State, "state"),
        new(SortKey.Pid, Text.Pid, Text.Pid, "pid"),
        new(SortKey.Name, $"{CommonVocabulary.Name} / {TechnicalTerms.Command}", CommonVocabulary.Name, "name-cell"),
        new(SortKey.RunningFor, Text.RunningFor, Text.RunningFor, "num"),
        new(SortKey.Threads, Text.Threads, Text.Threads, "num"),
        new(SortKey.Memory, Text.ResMb, Text.ResMb, "num"),
        new(null, Text.CpuHistory, Text.CpuHistory, "num chart"),
        new(SortKey.Cpu, TechnicalAcronyms.Cpu, TechnicalAcronyms.Cpu, "num"),
    ];

    private string CountText => string.IsNullOrWhiteSpace(_filter)
        ? string.Format(CultureInfo.CurrentCulture, Text.ProcessCount, _rows.Count)
        : string.Format(CultureInfo.CurrentCulture, Text.MatchCount, _rows.Count);

    /// <summary>
    /// Opens the details of a process and moves it into view, clearing a search that would hide it.
    /// </summary>
    public void Reveal(int pid)
    {
        _filter = null;
        for (var parent = Parent(pid); parent is not null; parent = Parent(parent.Info.Pid))
            _collapsedPids.Remove(parent.Info.Pid);

        _openPids.Add(pid);
        _pidToFocus = pid;
        BuildRows();
        StateHasChanged();
    }

    protected override void OnParametersSet()
    {
        IndexProcesses();
        _openPids.IntersectWith(_byPid.Keys);

        if (!_pointerInTable && !_focusInTable && _openPids.Count == 0)
            Resort();

        BuildRows();
    }

    private void IndexProcesses()
    {
        _byPid.Clear();
        _childCounts.Clear();
        _overThresholdCount = 0;
        _kernelThreadCount = 0;

        foreach (var process in Processes)
        {
            _byPid[process.Info.Pid] = process;
            _childCounts[process.Info.PPid] = _childCounts.GetValueOrDefault(process.Info.PPid) + 1;

            if (process.Status != OverviewStatus.Normal)
                _overThresholdCount++;
            if (process.IsKernelThread)
                _kernelThreadCount++;
        }

        foreach (var pid in _rowReferences.Keys.Where(pid => !_byPid.ContainsKey(pid)).ToList())
            _rowReferences.Remove(pid);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (_pidToFocus is { } pid && _rowReferences.TryGetValue(pid, out var row))
        {
            _pidToFocus = null;
            await row.FocusAsync();
        }
    }

    private void Resort()
    {
        var sorted = Processes.ToList();
        sorted.Sort(Compare);

        _rank.Clear();
        for (var i = 0; i < sorted.Count; i++)
            _rank[sorted[i].Info.Pid] = i;
    }

    private int Compare(ProcessEntry a, ProcessEntry b)
    {
        var result = _sortKey switch
        {
            SortKey.Status => a.Status.CompareTo(b.Status),
            SortKey.Pid => a.Info.Pid.CompareTo(b.Info.Pid),
            SortKey.Name => string.Compare(a.Info.Name, b.Info.Name, StringComparison.OrdinalIgnoreCase),
            SortKey.RunningFor => Nullable.Compare(a.RunningFor, b.RunningFor),
            SortKey.Threads => a.Info.Threads.CompareTo(b.Info.Threads),
            SortKey.Memory => Math.Round(a.Info.MemoryDetails.VmRSSKb / 1024d, 1).CompareTo(Math.Round(b.Info.MemoryDetails.VmRSSKb / 1024d, 1)),
            _ => Math.Round(a.CpuUsage, 1).CompareTo(Math.Round(b.CpuUsage, 1)),
        };

        if (_sortDescending)
            result = -result;

        return result != 0 ? result : CompareByRank(a, b);
    }

    private int CompareByRank(ProcessEntry a, ProcessEntry b)
    {
        var result = Rank(a).CompareTo(Rank(b));
        return result != 0 ? result : a.Info.Pid.CompareTo(b.Info.Pid);
    }

    private int Rank(ProcessEntry process)
        => _rank.TryGetValue(process.Info.Pid, out var rank) ? rank : int.MaxValue;

    private void BuildRows()
    {
        var visible = Processes.Where(Matches).ToList();
        visible.Sort(CompareByRank);

        _rows = _tree ? BuildTree(visible) : [.. visible.Select(p => new Row(p, 0, false))];
    }

    /// <summary>
    /// The matching processes with all their ancestors, so a match deep in the tree keeps its path.
    /// </summary>
    private List<Row> BuildTree(List<ProcessEntry> visible)
    {
        var shown = new HashSet<int>();
        foreach (var process in visible)
        {
            for (var current = process; current is not null && shown.Add(current.Info.Pid); current = Parent(current.Info.Pid))
            {
            }
        }

        var children = Processes
            .Where(p => shown.Contains(p.Info.Pid))
            .OrderBy(p => p, Comparer<ProcessEntry>.Create(CompareByRank))
            .ToLookup(p => shown.Contains(p.Info.PPid) ? p.Info.PPid : 0);

        var rows = new List<Row>();
        AddChildren(0, 0);
        return rows;

        void AddChildren(int parentPid, int depth)
        {
            foreach (var process in children[parentPid])
            {
                var hasChildren = children[process.Info.Pid].Any();
                rows.Add(new(process, depth, hasChildren));

                if (hasChildren && (!IsCollapsed(process.Info.Pid) || !string.IsNullOrWhiteSpace(_filter)))
                    AddChildren(process.Info.Pid, depth + 1);
            }
        }
    }

    private bool Matches(ProcessEntry process)
    {
        if (_groupFilter is { } group && process.Group != group)
            return false;

        if (process.IsKernelThread && !_showKernelThreads && _groupFilter != ProcessGroup.Kernel)
            return false;

        if (string.IsNullOrWhiteSpace(_filter))
            return true;

        return process.Info.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase)
            || process.Info.CmdLineContains(_filter)
            || process.Info.State.Contains(_filter, StringComparison.OrdinalIgnoreCase)
            || process.Info.Pid.ToString(CultureInfo.InvariantCulture) == _filter.Trim();
    }

    private ProcessEntry? Parent(int pid)
        => _byPid.TryGetValue(pid, out var process) && _byPid.TryGetValue(process.Info.PPid, out var parent) ? parent : null;

    private bool IsCollapsed(int pid)
        => _collapsedPids.Contains(pid);

    private void ApplyFilter(string? filter)
    {
        _filter = filter;
        BuildRows();
    }

    private void OnGroupSelected(ProcessGroup? group)
    {
        _groupFilter = group;
        BuildRows();
    }

    private void ToggleKernelThreads()
    {
        _showKernelThreads = !_showKernelThreads;
        BuildRows();
    }

    private void SetTree(bool tree)
    {
        _tree = tree;
        BuildRows();
    }

    private void SortBy(SortKey key)
    {
        if (key == _sortKey)
        {
            _sortDescending = !_sortDescending;
        }
        else
        {
            _sortKey = key;
            _sortDescending = key is SortKey.Threads or SortKey.Memory or SortKey.Cpu;
        }

        Resort();
        BuildRows();
    }

    private void ToggleDetails(int pid)
    {
        if (!_openPids.Remove(pid))
            _openPids.Add(pid);

        BuildRows();
    }

    private void ToggleCollapsed(int pid)
    {
        if (!_collapsedPids.Remove(pid))
            _collapsedPids.Add(pid);

        BuildRows();
    }

    private void OnRowKeyDown(KeyboardEventArgs args, int pid)
    {
        if (args.Key == "Enter")
            ToggleDetails(pid);
    }

    private string? AriaSort(SortKey key)
        => key != _sortKey ? null : _sortDescending ? "descending" : "ascending";

    private string SortArrow(SortKey key)
        => key != _sortKey ? string.Empty : _sortDescending ? " ↓" : " ↑";

    private static string StatusTitle(ProcessEntry process) => process.Status switch
    {
        OverviewStatus.Failed => Text.StateZombie,
        OverviewStatus.Warning when process.StateCode == 'D' => Text.StateDiskSleep,
        OverviewStatus.Warning => string.Format(CultureInfo.CurrentCulture, Text.OverThresholdOf, Percent(ProcessOverviewThresholds.ProcessCpuPercent)),
        _ => Text.WithinLimits,
    };

    private static string StateText(char stateCode) => stateCode switch
    {
        'R' => Text.StateRunning,
        'S' => Text.StateSleeping,
        'I' => Text.StateIdle,
        'D' => Text.StateDiskSleep,
        'Z' => Text.StateZombie,
        'T' or 't' => Text.StateStopped,
        _ => CommonVocabulary.Unknown,
    };

    private string ParentText(ProcessEntry process)
        => _byPid.TryGetValue(process.Info.PPid, out var parent)
            ? $"{parent.Info.Pid} {parent.Info.Name}"
            : process.Info.PPid == 0 ? "–" : process.Info.PPid.ToString(CultureInfo.CurrentCulture);

    private int ChildCount(int pid)
        => _childCounts.GetValueOrDefault(pid);

    private static string StartedText(ProcessEntry process)
        => process.Info.StartTime is { } start
            ? string.Format(CultureInfo.CurrentCulture, Text.StartedAt, start.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
                OverviewFormat.Duration(process.RunningFor))
            : "–";

    private static string MemoryText(ProcessEntry process)
    {
        var memory = process.Info.MemoryDetails;
        return string.Format(CultureInfo.CurrentCulture, Text.MemoryDetails,
            MegabytesFromKilobytes(memory.VmRSSKb), MegabytesFromKilobytes(memory.VmSizeKb), MegabytesFromKilobytes(memory.VmPeakKb),
            MegabytesFromKilobytes(memory.VmSwapKb));
    }

    private static string CpuText(ProcessEntry process)
    {
        var samples = process.Cpu.Samples;
        return string.Format(CultureInfo.CurrentCulture, Text.CpuDetails,
            Percent(process.CpuUsage), Percent(samples.Max()), Percent(samples.Average()));
    }

    private string MemoryShare(ProcessEntry process)
        => TotalMemoryKb > 0
            ? Math.Clamp(process.Info.MemoryDetails.VmRSSKb / TotalMemoryKb * 100, 0, 100).ToString("0.#", CultureInfo.InvariantCulture)
            : "0";

    /// <summary>
    /// <c>Key</c> is <c>null</c> for a column that cannot be sorted; <c>Label</c> names it for screen readers.
    /// </summary>
    private sealed record Column(SortKey? Key, string Header, string Label, string CssClass);

    private sealed record Row(ProcessEntry Process, int Depth, bool HasChildren);
}
