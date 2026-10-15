namespace Blazor.Shared.SystemInformation.Models;

/// <summary>
/// One minute of the system monitoring; a value is <c>null</c> when its collector reported nothing.
/// </summary>
/// <param name="Network">Share of the link speed; <c>null</c> on interfaces without a link speed, such as Wi-Fi.</param>
/// <param name="NetworkMbit">Received and sent together, for interfaces without a link speed.</param>
public sealed record SystemMonitoringSample(DateTimeOffset Time, double? Cpu, double? Memory, double? Disk, double? Network, double? NetworkMbit = null);
