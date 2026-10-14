using System.Globalization;
using System.Net;
using System.Text;
using Blazor.Shared.SystemInformation.Components.ProcessOverview;
using Blazor.Shared.SystemInformation.Extensions;
using Blazor.Shared.SystemInformation.Models;
using Blazor.Shared.SystemInformation.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Client.Services;
using Sdk.Instance;
using ViciOne.Ui.Localization.Resources;
using static Blazor.Shared.SystemInformation.Components.ProcessOverview.OverviewFormat;
using Text = Blazor.Shared.SystemInformation.Components.Localization.ProcessComponent;

namespace Blazor.Shared.SystemInformation.Components;

/// <summary>
/// Utilization and processes of the local instance.
/// </summary>
public sealed partial class ProcessComponent : IDisposable
{
    private static readonly MetricHistory s_noHistory = new([], ProcessOverviewSampler.HistoryLength);

    private readonly HashSet<string> _expandedGroups = [];

    private ProcessOverviewSnapshot? _snapshot;
    private ProcessTable? _table;
    private IDisposable? _subscription;
    private CultureInfo _culture = CultureInfo.CurrentCulture;
    private CultureInfo _uiCulture = CultureInfo.CurrentUICulture;
    private Tile[] _tiles = [];
    private SystemRow[] _systemRows = [];
    private IReadOnlyList<LoadPeak> _peaks = [];
    private IReadOnlyList<MinuteLoad> _minutes = [];
    private IReadOnlySet<int> _runningPids = new HashSet<int>();
    private LoadPeak? _openPeak;
    private bool _peakDialogVisible;

    [Inject] private ProcessOverviewSampler Sampler { get; set; } = default!;

    [Inject] private HardwareInfoService HardwareInfo { get; set; } = default!;

    [Inject] private DailyRangeService DailyRanges { get; set; } = default!;

    [Inject] private IInstanceInformationProvider InstanceInformation { get; set; } = default!;

    [Inject] private ILayoutService LayoutService { get; set; } = default!;



    [Inject(Key = Sdk.Constants.ClientTimeProviderServiceKey)] private TimeProvider TimeProvider { get; set; } = default!;

    private SystemMetrics Metrics => _snapshot!.System;

    private string InstanceName => InstanceInformation.Local.Name ?? string.Empty;

    private string InstanceType => InstanceInformation.Local.Type.ToString();

    private static int Cores => Environment.ProcessorCount;

    private double TotalMemoryKb => HardwareInfo.TotalMemoryKb;

    private double TotalMemoryGb => TotalMemoryKb / 1024 / 1024;

    private double? UsedMemoryGb => Metrics.Memory.Latest is { } usage && TotalMemoryKb > 0 ? usage / 100 * TotalMemoryGb : null;

    /// <summary>
    /// The size reported by the one-minute monitoring, or derived from free space and usage until that arrived.
    /// </summary>
    private double? TotalDiskGb
    {
        get
        {
            if (HardwareInfo.TotalDiskSizeGb > 0)
                return HardwareInfo.TotalDiskSizeGb;

            return Metrics.DiskFreeGb is { } free && Metrics.Disk.Latest is { } usage and < 100
                ? free / (1 - (usage / 100))
                : null;
        }
    }

    /// <summary>
    /// Used, cache and free add up to the total memory.
    /// </summary>
    private string? MemoryBreakdown
    {
        get
        {
            if (UsedMemoryGb is not { } used || Metrics.MemoryFreePercent is not { } freePercent)
                return null;

            var free = freePercent / 100 * TotalMemoryGb;
            var cache = Math.Max(TotalMemoryGb - used - free, 0);
            return string.Format(CultureInfo.CurrentCulture, Text.MemoryBreakdown, Gigabytes(used), Gigabytes(cache), Gigabytes(free));
        }
    }

    private bool HasNetworkUsage => Metrics.Network.Samples.Count > 0;

    /// <summary>
    /// Without peaks: that it stayed calm, and when the CPU was highest and who used it most then.
    /// </summary>
    private (string Lead, string? Value, string Tail) CalmText
    {
        get
        {
            var running = TimeProvider.GetUtcNow() - DailyRanges.Started;
            var calm = running < TimeSpan.FromHours(24) ? Text.NoPeakSinceStart : Text.NoPeakLastDay;
            var highest = _minutes.Where(m => m.Cpu is not null).MaxBy(m => m.Cpu);
            if (highest is null)
                return (calm, null, string.Empty);

            var value = string.Format(CultureInfo.CurrentCulture, Text.HighestCpuAt, PeakText.Percent(highest.Cpu!.Value), LocalTime(highest.Time));
            var tail = new StringBuilder();
            var atStart = highest.Time - DailyRanges.Started < TimeSpan.FromMinutes(3);
            if (atStart)
                tail.Append(' ').Append(Text.AtSuiteStart);
            if (highest.TopByCpu.FirstOrDefault() is { } top && !(atStart && top.Role == SuiteProcessRole.Suite))
                tail.Append(", ").Append(string.Format(CultureInfo.CurrentCulture, Text.MostlyBy, PeakText.Process(top)));
            return ($"{calm} {Text.HighestCpu} ", value, tail.Append('.').ToString());
        }
    }

    /// <summary>
    /// The span of the peaks chart: the time since the start until a whole day is collected.
    /// </summary>
    private string PeakSpanText
    {
        get
        {
            var running = TimeProvider.GetUtcNow() - DailyRanges.Started;
            return running < TimeSpan.FromHours(24)
                ? string.Format(CultureInfo.CurrentCulture, Text.SinceStart, Duration(running))
                : Text.LastDay;
        }
    }

    private string CpuSubtitle
    {
        get
        {
            var cores = string.Format(CultureInfo.CurrentCulture, Text.CoreCount, Cores);
            return Metrics.Clock is { } clock
                ? $"{cores} · {string.Format(CultureInfo.CurrentCulture, Text.CpuClock, Gigahertz(clock.CurrentMhz), Gigahertz(clock.MaximumMhz))}"
                : cores;
        }
    }

    private int SystemOverThreshold => _systemRows.Count(r => r.Status is OverviewStatus.Warning or OverviewStatus.Failed);

    private string HardwareDescription => string.Join(" · ", new[]
    {
        string.IsNullOrWhiteSpace(HardwareInfo.Processor) ? null : HardwareInfo.Processor,
        string.Format(CultureInfo.CurrentCulture, Text.CoreCount, Cores),
        TotalMemoryKb > 0 ? string.Format(CultureInfo.CurrentCulture, Text.MemorySize, Gigabytes(TotalMemoryGb)) : null,
        TotalDiskGb is { } disk ? string.Format(CultureInfo.CurrentCulture, Text.DiskSize, Gigabytes(disk)) : null,
    }.Where(part => part is not null));

    private string UptimeText
    {
        get
        {
            if (Metrics.UptimeSeconds is not { } seconds)
                return "–";

            var uptime = TimeSpan.FromSeconds(seconds);
            return string.Format(CultureInfo.CurrentCulture, Text.UptimeValue, (int)uptime.TotalDays, uptime.Hours, uptime.Minutes);
        }
    }

    private string UpdatedText
        => TimeZoneInfo.ConvertTime(_snapshot!.Timestamp, TimeProvider.LocalTimeZone).ToString("T", CultureInfo.CurrentCulture);

    private TileCaption? DiskCaption => Metrics.DiskFreeGb is { } free
        ? new(new(null, $"{Gigabytes(free)} GB", Text.CaptionFree), new(null, Metrics.Disk.Latest is { } usage ? Percent(usage) : "–", Text.CaptionUsed))
        : null;

    private (string Arrow, string Text) LoadTrend => (Metrics.LoadAverage.Latest - Metrics.LoadAverage5) switch
    {
        > 0.05 => ("↗", Text.TrendRising),
        < -0.05 => ("↘", Text.TrendFalling),
        _ => ("→", Text.TrendSteady),
    };

    protected override void OnInitialized()
    {
        LayoutService.TitleBarAppName = Text.ProcessOverview;

        _culture = CultureInfo.CurrentCulture;
        _uiCulture = CultureInfo.CurrentUICulture;
        _subscription = Sampler.Subscribe();
        ApplySnapshot(Sampler.Current);
        Sampler.Changed += OnSamplerChanged;
    }

    public void Dispose()
    {
        Sampler.Changed -= OnSamplerChanged;
        _subscription?.Dispose();
    }

    private Task<string> GetSnapshotText()
        => Task.FromResult(BuildSnapshot());

    /// <summary>
    /// Markdown for a ticket with everything the page shows at this moment, all processes included.
    /// </summary>
    private string BuildSnapshot()
    {
        var culture = CultureInfo.CurrentCulture;
        var time = TimeZoneInfo.ConvertTime(_snapshot!.Timestamp, TimeProvider.LocalTimeZone).ToString("g", culture);
        var text = new StringBuilder()
            .AppendLine(culture, $"## {string.Format(culture, Text.SnapshotTitle, InstanceName, time)}")
            .AppendLine()
            .AppendLine(culture, $"{InstanceType} · {HardwareDescription} · {Text.Uptime} {UptimeText}")
            .AppendLine()
            .AppendLine(culture, $"### {Text.SystemValues}")
            .AppendLine();

        foreach (var row in _systemRows)
            text.AppendLine(culture, $"{(row.Indented ? "  " : null)}- {row.Title}: {row.Value}{(row.Subtitle is null ? null : $" · {row.Subtitle}")}");

        text.AppendLine()
            .AppendLine(culture, $"### {CommonVocabulary.GroupPlural}")
            .AppendLine()
            .AppendLine(culture, $"| {CommonVocabulary.GroupPlural} | {TechnicalAcronyms.Cpu} | {Text.ResMb} |")
            .AppendLine("|---|---|---|");

        foreach (var group in _snapshot.Processes.GroupBy(p => p.Group).OrderBy(g => g.Key))
        {
            text.AppendLine(culture,
                $"| {group.Key.DisplayName} | {Percent(group.Sum(p => p.CpuUsage))} | {MegabytesFromKilobytes(group.Sum(p => p.Info.MemoryDetails.VmRSSKb))} |");
        }

        text.AppendLine()
            .AppendLine(culture, $"### {string.Format(culture, Text.ProcessCount, _snapshot.Processes.Count)}")
            .AppendLine()
            .AppendLine(culture,
                $"| {Text.Pid} | PPID | {CommonVocabulary.Name} | {CommonVocabulary.State} | {Text.Threads} | {TechnicalAcronyms.Cpu} | {Text.ResMb} | VIRT (MB) | Swap (MB) | {TechnicalTerms.Command} |")
            .AppendLine("|---|---|---|---|---|---|---|---|---|---|");

        foreach (var process in _snapshot.Processes.OrderByDescending(p => p.CpuUsage).ThenBy(p => p.Info.Pid))
        {
            var info = process.Info;
            var memory = info.MemoryDetails;
            text.AppendLine(culture,
                $"| {info.Pid} | {info.PPid} | {Cell(info.Name)} | {Cell(info.State)} | {info.Threads} | {Percent(process.CpuUsage)} | " +
                $"{MegabytesFromKilobytes(memory.VmRSSKb)} | {MegabytesFromKilobytes(memory.VmSizeKb)} | {MegabytesFromKilobytes(memory.VmSwapKb)} | {Cell(info.CmdLine)} |");
        }

        return text.ToString();
    }

    /// <summary>
    /// A pipe would end the Markdown table cell.
    /// </summary>
    private static string Cell(string? value)
        => string.IsNullOrEmpty(value) ? "–" : value.Replace("|", "\\|", StringComparison.Ordinal);

    // The sampler raises Changed on its own thread; rendering there would use its culture instead of this user's.
    private void OnSamplerChanged()
        => _ = InvokeAsync(() =>
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _uiCulture;
            ApplySnapshot(Sampler.Current);
            StateHasChanged();
        });

    private void ApplySnapshot(ProcessOverviewSnapshot? snapshot)
    {
        _snapshot = snapshot;
        if (snapshot is null)
            return;

        _systemRows = BuildSystemRows();
        _tiles = BuildTiles();
        _peaks = DailyRanges.Peaks;
        _minutes = DailyRanges.Minutes();
        _runningPids = snapshot.Processes.Select(p => p.Info.Pid).ToHashSet();
    }

    private Tile[] BuildTiles()
    {
        var ranges = DailyRanges.Current;
        var memoryTotal = TotalMemoryKb > 0 ? $"/ {Gigabytes(TotalMemoryGb)} GB" : null;

        return
        [
            RangeTile(Number(Metrics.Cpu.Latest), "%", TechnicalAcronyms.Cpu, Metrics.Cpu, 0, 100, ProcessOverviewThresholds.CpuPercent,
                ranges.Cpu, Percent) with
            {
                Alert = Metrics.Clock?.LimitMhz is { } limit ? string.Format(CultureInfo.CurrentCulture, Text.ClockLimited, Gigahertz(limit)) : null,
            },
            RangeTile(UsedMemoryGb is { } used ? Gigabytes(used) : "–", memoryTotal, Text.Memory, Metrics.Memory, 0, 100,
                ProcessOverviewThresholds.MemoryPercent, ranges.Memory, MemoryShareAsGigabytes) with
            {
                Alert = Metrics.Refaults.Latest > ProcessOverviewThresholds.RefaultsPerSecond ? Text.MemoryPressure : null,
            },
            DiskTile(ranges.Disk),
            NetworkTile(ranges),
            LoadAverageTile(),
            TemperatureTile(ranges.Temperature),
            DiskWriteTile(ranges),
            ProcessesTile(),
        ];
    }

    /// <summary>
    /// With a second sensor, the CPU stays the main value and the other one is shown in the corner.
    /// </summary>
    private Tile TemperatureTile(ValueRange? range)
    {
        if (Metrics.Temperature.Latest is null)
            return new("–", null, Text.Temperature, false);

        var tile = RangeTile(Number(Metrics.Temperature.Latest), "°C", Text.Temperature, Metrics.Temperature, 30, 90,
            ProcessOverviewThresholds.TemperatureCelsius, range, v => $"{Number(v)} °C") with
        {
            Alert = Metrics.ThrottlingPercent > 0 ? Text.Throttling : null,
        };
        if (Metrics.ThrottleFromCelsius is { } throttleFrom && tile.Gauge is { } gauge)
            tile = tile with { Critical = gauge.Current > throttleFrom };
        return Metrics.SecondTemperature.Latest is { } second
            ? tile with { Label = Text.TemperatureCpu, Corner = new(SensorName(Metrics.SecondTemperatureSensor), $"{Number(second)} °C", null) }
            : tile;
    }

    /// <summary>
    /// The used space in GB like the memory tile, once the size is known; the share follows in the caption.
    /// </summary>
    private Tile DiskTile(ValueRange? range)
    {
        var tile = RangeTile(Number(Metrics.Disk.Latest), "%", Text.Disk, Metrics.Disk, 0, 100, ProcessOverviewThresholds.DiskPercent,
            range, Percent) with { Caption = DiskCaption };
        return TotalDiskGb is { } total && Metrics.DiskFreeGb is { } free
            ? tile with { Value = Gigabytes(Math.Max(total - free, 0)), Unit = $"/ {Gigabytes(total)} GB" }
            : tile;
    }

    private Tile DiskWriteTile(DailyRanges ranges)
    {
        if (Metrics.DiskWrite.Latest is not { } current)
            return new("–", null, Text.DiskWrite, false, Text.DiskWriteHint);

        var (value, unit) = ByteRate(current);
        var peak = Math.Max(ranges.DiskWrite?.Max ?? 0, Metrics.DiskWrite.Samples.Max());
        var (peakValue, peakUnit) = ByteRate(peak);
        var caption = new TileCaption(
            new(Text.CaptionDay, ranges.WrittenBytes is { } written ? ByteAmount(written) : "–", null),
            new(Text.CaptionMax, $"{peakValue} {peakUnit}", null));
        return new(value, unit, Text.DiskWrite, false, Text.DiskWriteHint, null, caption);
    }

    private Tile ProcessesTile()
    {
        var processes = _snapshot!.Processes;
        var caption = new TileCaption(
            new(null, processes.Sum(p => p.Info.Threads).ToString("N0", CultureInfo.CurrentCulture), Text.Threads),
            new(null, processes.Count(p => p.StateCode == 'R').ToString("N0", CultureInfo.CurrentCulture), Text.ProcessesRunning));
        return new(processes.Count.ToString("N0", CultureInfo.CurrentCulture), null, Text.Processes, false, null, null, caption);
    }

    private static string SensorName(string? sensor)
        => sensor?.StartsWith("lm75", StringComparison.Ordinal) == true ? Text.Board : sensor ?? string.Empty;

    private static (string Value, string Unit) ByteRate(double bytesPerSecond)
        => bytesPerSecond >= 1024 * 1024
            ? (Number(bytesPerSecond / 1024 / 1024), "MB/s")
            : (Number(bytesPerSecond / 1024, "N0"), "KB/s");

    private static string ByteAmount(double bytes)
        => bytes switch
        {
            >= 1024d * 1024 * 1024 => $"{Number(bytes / 1024 / 1024 / 1024, "N2")} GB",
            >= 1024d * 1024 => $"{Number(bytes / 1024 / 1024, "N0")} MB",
            _ => $"{Number(bytes / 1024, "N0")} KB",
        };

    private SystemRow[] BuildSystemRows()
    {
        var rows = new List<SystemRow>
        {
            PercentRow(TechnicalAcronyms.Cpu, CpuSubtitle, Metrics.Cpu, ProcessOverviewThresholds.CpuPercent),
        };

        var coreClocks = Metrics.Clock?.CoreMhz ?? [];
        rows.AddRange(Metrics.CoreCpu.Select((core, i) => PercentRow(
            string.Format(CultureInfo.CurrentCulture, Text.CoreNumber, i + 1),
            i < coreClocks.Count ? $"{Gigahertz(coreClocks[i])} GHz" : null,
            core, ProcessOverviewThresholds.CpuPercent)
            with { Parent = TechnicalAcronyms.Cpu, Bar = core.Latest }));

        if (Metrics.IoWait.Samples.Count > 0)
            rows.Add(PercentRow(Text.IoWait, null, Metrics.IoWait, ProcessOverviewThresholds.IoWaitPercent) with { Parent = TechnicalAcronyms.Cpu });

        rows.AddRange(
        [
            PercentRow(Text.Memory, MemoryBreakdown, Metrics.Memory, ProcessOverviewThresholds.MemoryPercent),
        ]);

        if (RefaultsRow() is { } refaults)
            rows.Add(refaults);

        if (SwapRow() is { } swap)
            rows.Add(swap);

        rows.AddRange(
        [
            PercentRow(Text.Disk,
                Metrics.DiskFreeGb is { } free ? string.Format(CultureInfo.CurrentCulture, Text.FreeGb, Gigabytes(free)) : null,
                Metrics.Disk, ProcessOverviewThresholds.DiskPercent),
            NetworkRow(),
        ]);

        rows.AddRange(NetworkDetailRows());
        rows.Add(LoadAverageRow());

        if (Metrics.Temperature.Samples.Count > 0)
        {
            var limit = ProcessOverviewThresholds.TemperatureCelsius;
            var throttleFrom = Metrics.ThrottleFromCelsius is { } trip
                ? string.Format(CultureInfo.CurrentCulture, Text.ThrottleFrom, Number(trip, "N0"))
                : null;
            rows.Add(new(Text.Temperature, string.Join(" · ", new[] { Metrics.TemperatureSensor, throttleFrom }.OfType<string>()), Metrics.Temperature, 100, limit,
                $"{Number(Metrics.Temperature.Latest)} °C", Threshold($"{Number(limit)} °C")));

            if (Metrics.SecondTemperature.Latest is { } second)
            {
                rows.Add(new(SensorName(Metrics.SecondTemperatureSensor), Metrics.SecondTemperatureSensor, Metrics.SecondTemperature, 100,
                    limit, $"{Number(second)} °C", Threshold($"{Number(limit)} °C")) { Parent = Text.Temperature });
            }
        }

        return [.. rows];
    }

    /// <summary>
    /// Without a link speed, as on Wi-Fi, the scale runs from 0 to the highest throughput of the day and has no threshold.
    /// </summary>
    private Tile NetworkTile(DailyRanges ranges)
    {
        var errors = Metrics.NetworkErrors.Samples.Sum();
        var alert = errors > 0 ? string.Format(CultureInfo.CurrentCulture, Text.NetworkErrorCount, Number(errors, "N0")) : null;
        var caption = new TileCaption(
            new(null, $"↓ {Number(Metrics.NetworkReceivedMbit)} ↑ {Number(Metrics.NetworkSentMbit)}", "Mbit/s"),
            new(null, string.Empty, null));
        string Mbit(double value) => $"{Number(value)} Mbit/s";

        var tile = HasNetworkUsage
            ? RangeTile(Number(Metrics.Network.Latest), "%", CommonVocabulary.Network, Metrics.Network, 0, 100,
                ProcessOverviewThresholds.NetworkPercent, ranges.Network, Percent)
            : RangeTile(Number(Metrics.NetworkThroughput.Latest), "Mbit/s", CommonVocabulary.Network, Metrics.NetworkThroughput, 0,
                Math.Max(1, Math.Max(ranges.NetworkThroughput?.Max ?? 0, Metrics.NetworkThroughput.Samples.DefaultIfEmpty(0).Max())),
                null, ranges.NetworkThroughput, Mbit);
        return tile with { Caption = caption, Alert = alert };
    }

    private Tile LoadAverageTile()
    {
        var load = Metrics.LoadAverage;
        var limit = ProcessOverviewThresholds.LoadAverage;
        var gauge = load.Latest is { } current
            ? new TileGauge(current, 0, limit * 2, null, limit, [.. new[] { Metrics.LoadAverage5, Metrics.LoadAverage15 }.OfType<double>()])
            : null;
        var caption = new TileCaption(new(Text.CaptionFiveMinutes, Number(Metrics.LoadAverage5, "N2"), null),
            new(Text.CaptionFifteenMinutes, Number(Metrics.LoadAverage15, "N2"), null));

        return new(Number(load.Latest, "N2"), string.Format(CultureInfo.CurrentCulture, Text.OfCores, Cores), Text.LoadAverageTile,
            IsOver(load, limit), Text.LoadAverageHint, gauge, caption, LoadTrend);
    }

    private static Tile RangeTile(string value, string? unit, string label, MetricHistory history, double lowerBound,
        double upperBound, double? limit, ValueRange? range, Func<double, string> format)
    {
        range = WithRecentSamples(range, history);
        var gauge = history.Latest is { } current ? new TileGauge(current, lowerBound, upperBound, range, limit, []) : null;
        var caption = new TileCaption(new(Text.CaptionMin, range is null ? "–" : format(range.Min), null),
            new(Text.CaptionMax, range is null ? "–" : format(range.Max), null));
        var tooltip = range is null
            ? null
            : string.Format(CultureInfo.CurrentCulture, Text.DayRangeHint, format(range.Min), format(range.Average), format(range.Max));

        return new(value, unit, label, limit is { } threshold && IsOver(history, threshold), tooltip, gauge, caption);
    }

    /// <summary>
    /// Adds the live samples, read at other moments than the one-minute monitoring, so the current value stays inside the range.
    /// </summary>
    private static ValueRange? WithRecentSamples(ValueRange? range, MetricHistory history)
    {
        if (history.Samples.Count == 0)
            return range;

        var min = history.Samples.Min();
        var max = history.Samples.Max();
        return range is null
            ? new(min, history.Samples.Average(), max)
            : range with { Min = Math.Min(range.Min, min), Max = Math.Max(range.Max, max) };
    }

    private static string Gigahertz(double megahertz)
        => (megahertz / 1000).ToString("N1", CultureInfo.CurrentCulture);

    private string MemoryShareAsGigabytes(double share)
        => TotalMemoryKb > 0 ? $"{Gigabytes(share / 100 * TotalMemoryGb)} GB" : Percent(share);

    private static MarkupString CaptionText(CaptionPart part)
        => new(string.Join(" ", new[]
        {
            part.Prefix is null ? null : WebUtility.HtmlEncode(part.Prefix),
            $"<b>{WebUtility.HtmlEncode(part.Value)}</b>",
            part.Suffix is null ? null : WebUtility.HtmlEncode(part.Suffix),
        }.Where(p => p is not null)));

    private SystemRow NetworkRow()
    {
        var subtitle = Metrics.NetworkSpeedMbit is > 0 and var speed
            ? $"{Metrics.NetworkInterface} · {Number(speed, "N0")} Mbit/s"
            : Metrics.NetworkInterface;

        if (HasNetworkUsage)
            return PercentRow(CommonVocabulary.Network, subtitle, Metrics.Network, ProcessOverviewThresholds.NetworkPercent);

        // Wi-Fi reports no link speed, so there is no share to compare with a threshold.
        var throughput = Metrics.NetworkThroughput;
        var max = Math.Max(1, throughput.Samples.DefaultIfEmpty(0).Max());
        return new(CommonVocabulary.Network, subtitle, throughput, max, null, $"{Number(throughput.Latest)} Mbit/s", null);
    }

    private IEnumerable<SystemRow> NetworkDetailRows()
    {
        if (Metrics.NetworkInterface is null)
            yield break;

        var speed = Metrics.NetworkSpeedMbit is > 0 and var linkSpeed ? linkSpeed : (double?)null;
        var max = speed ?? Math.Max(1, Metrics.NetworkReceived.Samples.Concat(Metrics.NetworkSent.Samples).DefaultIfEmpty(0).Max());
        var ranges = DailyRanges.Current;
        foreach (var (title, history, total) in new[]
                 {
                     (Text.NetworkReceived, Metrics.NetworkReceived, ranges.ReceivedBytes),
                     (Text.NetworkSent, Metrics.NetworkSent, ranges.SentBytes),
                 })
        {
            var subtitle = total is { } bytes ? string.Format(CultureInfo.CurrentCulture, Text.AmountInDay, ByteAmount(bytes)) : null;
            yield return new SystemRow(title, subtitle, history, max, null, $"{Number(history.Latest)} Mbit/s", null)
            {
                Parent = CommonVocabulary.Network,
                Bar = speed is { } s && history.Latest is { } current ? current / s * 100 : null,
            };
        }

        var errors = Metrics.NetworkErrors;
        var recent = errors.Samples.Sum();
        var dropped = string.Format(CultureInfo.CurrentCulture, Text.NetworkErrorsDetails, Number(Metrics.NetworkDropped.Samples.Sum(), "N0"));
        yield return new SystemRow(Text.NetworkErrors, dropped, errors, Math.Max(5, errors.Samples.DefaultIfEmpty(0).Max()),
            null, Number(recent, "N0"), Text.NetworkErrorsHint)
        {
            Parent = CommonVocabulary.Network,
            StatusOverride = recent > 0 ? OverviewStatus.Warning : OverviewStatus.Normal,
        };

        foreach (var port in Metrics.NetworkPorts)
        {
            yield return new SystemRow(string.Format(CultureInfo.CurrentCulture, Text.NetworkPort, port.Name),
                port.Connected ? Text.PortConnected : Text.PortNoCable, s_noHistory, 1, null,
                port.SpeedMbit is { } portSpeed ? $"{Number(portSpeed, "N0")} Mbit/s" : "–", null)
            {
                Parent = CommonVocabulary.Network,
                StatusOverride = port.Connected ? OverviewStatus.Normal : OverviewStatus.Inactive,
            };
        }
    }

    /// <summary>
    /// Only while the monitoring reports it; without swap this is where memory pressure shows.
    /// </summary>
    private SystemRow? RefaultsRow()
    {
        var faults = Metrics.Refaults;
        if (faults.Latest is not { } latest)
            return null;

        var limit = ProcessOverviewThresholds.RefaultsPerSecond;
        return new SystemRow(Text.Refaults, null, faults, Math.Max(limit * 2, faults.Samples.Max()), limit,
            string.Format(CultureInfo.CurrentCulture, Text.PagesPerSecond, Number(latest, "N0")), Text.RefaultsHint)
        {
            Parent = Text.Memory,
        };
    }

    /// <summary>
    /// Only while the device has swap space.
    /// </summary>
    private SystemRow? SwapRow()
    {
        if (Metrics.SwapTotalKb is not > 0 || Metrics.Swap.Latest is not { } used)
            return null;

        var pages = Metrics.SwapPages.Samples.Sum();
        var subtitle = string.Format(CultureInfo.CurrentCulture, Text.SwapDetails,
            MegabytesFromKilobytes((long)(Metrics.SwapUsedKb ?? 0)), MegabytesFromKilobytes((long)Metrics.SwapTotalKb.Value), Number(pages, "N0"));
        return new SystemRow(Text.Swap, subtitle, Metrics.Swap, 100, null, Percent(used), Text.SwapHint)
        {
            Parent = Text.Memory,
            Bar = used,
            StatusOverride = pages > 0 ? OverviewStatus.Warning : OverviewStatus.Normal,
        };
    }

    private IEnumerable<SystemRow> VisibleSystemRows()
        => _systemRows.Where(r => r.Parent is null || _expandedGroups.Contains(r.Parent));

    private bool HasChildRows(SystemRow row)
        => !row.Indented && _systemRows.Any(r => r.Parent == row.Title);

    /// <summary>
    /// A collapsed row also shows the most severe status of the rows it hides.
    /// </summary>
    private OverviewStatus ShownStatus(SystemRow row)
        => HasChildRows(row) && !_expandedGroups.Contains(row.Title)
            ? _systemRows.Where(r => r.Parent == row.Title).Select(r => r.Status).Append(row.Status).Min()
            : row.Status;

    private void ToggleGroup(string title)
    {
        if (!_expandedGroups.Remove(title))
            _expandedGroups.Add(title);
    }

    private SystemRow LoadAverageRow()
    {
        var load = Metrics.LoadAverage;
        var limit = ProcessOverviewThresholds.LoadAverage;
        var subtitle = string.Format(CultureInfo.CurrentCulture, Text.LoadAverageDetails,
            Number(load.Latest, "N2"), Number(Metrics.LoadAverage5, "N2"), Number(Metrics.LoadAverage15, "N2"));
        var max = Math.Max(limit * 1.5, load.Samples.DefaultIfEmpty(0).Max());

        return new(Text.LoadAverage, subtitle, load, max, limit, Number(load.Latest, "N2"),
            $"{Text.LoadAverageHint} {Threshold(Number(limit, "N2"))}");
    }

    private static SystemRow PercentRow(string title, string? subtitle, MetricHistory history, double limit)
        => new(title, subtitle, history, 100, limit, history.Latest is null ? "–" : Percent(history.Latest.Value), Threshold(Percent(limit)));

    private static bool IsOver(MetricHistory history, double limit)
        => OverviewStatusExtensions.StatusFor(history.Latest, limit) != OverviewStatus.Normal;

    private static string Threshold(string value)
        => string.Format(CultureInfo.CurrentCulture, Text.ThresholdValue, value);

    /// <summary>
    /// The largest consumer of the highest minute and how long the peak lasted; memory pressure names the largest by memory.
    /// </summary>
    private static string PeakSubtitle(LoadPeak peak)
    {
        var duration = $"{peak.Minutes.ToString(CultureInfo.CurrentCulture)} min";
        if (peak.TopProcesses.Count == 0)
            return duration;

        var top = peak.TopProcesses[0];
        var share = peak.Kind == PeakKind.MemoryPressure ? $"{MegabytesFromKilobytes(top.MemoryKb)} MB" : PeakText.Percent(top.CpuPercent);
        return $"{PeakText.Process(top)} {share} · {duration}";
    }

    private string LocalTime(DateTimeOffset time)
        => TimeZoneInfo.ConvertTime(time, TimeProvider.LocalTimeZone).ToString("HH:mm", CultureInfo.InvariantCulture);

    private void OpenPeak(LoadPeak peak)
    {
        _openPeak = peak;
        _peakDialogVisible = true;
    }

    /// <summary>
    /// <c>Unit</c> follows the value in a smaller font, such as "%" or "/ 1,90 GB".
    /// </summary>
    /// <remarks>
    /// <c>Alert</c> replaces the right caption with a warning chip; <c>Corner</c> is a second value next to the main one.
    /// </remarks>
    private sealed record Tile(string Value, string? Unit, string Label, bool Over, string? Tooltip = null,
        TileGauge? Gauge = null, TileCaption? Caption = null, (string Arrow, string Text)? Trend = null, string? Alert = null,
        CaptionPart? Corner = null, bool Critical = false);

    private sealed record TileGauge(double Current, double LowerBound, double UpperBound, ValueRange? Range, double? Limit,
        IReadOnlyList<double> Marks);

    /// <summary>
    /// Two short values under the gauge, one at each end.
    /// </summary>
    private sealed record TileCaption(CaptionPart Left, CaptionPart Right);

    /// <summary>
    /// <c>Value</c> is emphasized; <c>Prefix</c> and <c>Suffix</c> name it, such as "min" or "free".
    /// </summary>
    private sealed record CaptionPart(string? Prefix, string Value, string? Suffix);

    /// <summary>
    /// <c>Limit</c> is <c>null</c> for values without a threshold, which are always normal.
    /// </summary>
    private sealed record SystemRow(string Title, string? Subtitle, MetricHistory History, double Max, double? Limit,
        string Value, string? Tooltip)
    {
        /// <summary>
        /// Title of the row this one belongs to, shown indented below it.
        /// </summary>
        public string? Parent { get; init; }

        public bool Indented => Parent is not null;

        public double? Bar { get; init; }

        public OverviewStatus? StatusOverride { get; init; }

        public OverviewStatus Status => StatusOverride ?? OverviewStatusExtensions.StatusFor(History.Latest, Limit);
    }
}
