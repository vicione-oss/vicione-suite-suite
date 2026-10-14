namespace Blazor.Shared.SystemInformation.Models;

/// <summary>
/// Values above which the process overview marks a metric or a process as a warning.
/// </summary>
internal static class ProcessOverviewThresholds
{
    public const double CpuPercent = 80;
    public const double MemoryPercent = 85;
    public const double DiskPercent = 90;
    public const double NetworkPercent = 80;
    public const double TemperatureCelsius = 75;

    /// <summary>
    /// Share of the CPU time spent waiting for the disk.
    /// </summary>
    public const double IoWaitPercent = 20;

    /// <summary>
    /// Pages read back from disk per second; a device with enough memory stays near zero.
    /// </summary>
    public const double RefaultsPerSecond = 50;

    /// <summary>
    /// Share of the whole system, so on two cores a process using one core fully shows 50 %.
    /// </summary>
    public const double ProcessCpuPercent = 25;

    /// <summary>
    /// Top of the process CPU charts.
    /// </summary>
    public const double ProcessChartMax = 50;

    /// <summary>
    /// A load average above the number of cores means processes wait for a core.
    /// </summary>
    public static double LoadAverage => Environment.ProcessorCount;
}
