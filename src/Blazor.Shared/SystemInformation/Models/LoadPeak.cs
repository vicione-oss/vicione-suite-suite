namespace Blazor.Shared.SystemInformation.Models;

public enum PeakKind
{
    Cpu,
    Temperature,
    MemoryPressure,
}

/// <summary>
/// One of the largest consumers of a minute.
/// </summary>
/// <param name="CpuPercent">Share of the whole system over that minute.</param>
public sealed record TopProcess(int Pid, string Name, SuiteProcessRole Role, double CpuPercent, long MemoryKb);

/// <summary>
/// One minute of the day with the values the peaks are found in and its largest consumers.
/// </summary>
/// <param name="Cpu">The average of the minute.</param>
/// <param name="CpuMin">The lowest of the readings within the minute; <c>null</c> without live readings.</param>
/// <param name="CpuMax">The highest of the readings within the minute; <c>null</c> without live readings.</param>
public sealed record MinuteLoad(
    DateTimeOffset Time,
    double? Cpu,
    double? Temperature,
    double? Refaults,
    double? ThrottlingPercent,
    IReadOnlyList<TopProcess> TopByCpu,
    IReadOnlyList<TopProcess> TopByMemory,
    double? CpuMin = null,
    double? CpuMax = null);

/// <summary>
/// Consecutive minutes above the warning threshold of a value.
/// </summary>
/// <param name="TopProcesses">The largest consumers of the highest minute: by CPU, or by memory for memory pressure.</param>
/// <param name="Throttled">Whether the kernel throttled during the peak.</param>
/// <param name="Course">The minutes around the highest one, for the chart in the details.</param>
public sealed record LoadPeak(
    PeakKind Kind,
    DateTimeOffset Start,
    int Minutes,
    DateTimeOffset PeakTime,
    double PeakValue,
    double Limit,
    bool Throttled,
    IReadOnlyList<TopProcess> TopProcesses,
    IReadOnlyList<(DateTimeOffset Time, double? Value)> Course);
