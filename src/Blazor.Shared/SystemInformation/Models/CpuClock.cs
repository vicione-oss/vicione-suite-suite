namespace Blazor.Shared.SystemInformation.Models;

/// <summary>
/// The clock of the CPU from cpufreq, in MHz.
/// </summary>
/// <param name="CurrentMhz">The average of all cores.</param>
/// <param name="MaximumMhz">The highest maximum of all cores.</param>
/// <param name="LimitMhz">The clock the kernel currently allows on the most limited core; <c>null</c> while every core may run at its maximum.</param>
/// <param name="CoreMhz">The current clock per core, in the order of the cores.</param>
public sealed record CpuClock(double CurrentMhz, double MaximumMhz, double? LimitMhz, IReadOnlyList<double> CoreMhz);
