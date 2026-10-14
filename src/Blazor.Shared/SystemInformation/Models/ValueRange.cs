namespace Blazor.Shared.SystemInformation.Models;

public sealed record ValueRange(double Min, double Average, double Max);

/// <summary>
/// Ranges of the last 24 hours, at most since the suite started; <c>null</c> until the first sample arrived.
/// </summary>
public sealed record DailyRanges(
    ValueRange? Cpu,
    ValueRange? Memory,
    ValueRange? Disk,
    ValueRange? Network,
    ValueRange? NetworkThroughput,
    ValueRange? Temperature,
    ValueRange? DiskWrite,
    double? WrittenBytes,
    double? ReceivedBytes,
    double? SentBytes);
