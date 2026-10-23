using Blazor.Shared.SystemInformation.Models;
using Blazor.Shared.SystemInformation.Services;
using ViciOne.SystemMonitoring;
using ViciOne.SystemMonitoring.Metrics;

namespace Blazor.Shared.Tests.SystemInformation.Services;

public sealed class MetricSelection_SelectSecondTemperature
{
    [Fact]
    public void Should_return_nothing_with_a_single_sensor()
        => MetricSelection.SelectSecondTemperature([Temperature("cpu_thermal_temp", 54.2)], "cpu_thermal_temp")
            .Should().Be((null, null));

    [Fact]
    public void Should_return_the_other_sensor()
        => MetricSelection.SelectSecondTemperature([Temperature("cpu_thermal_temp", 54.2), Temperature("lm75_temp", 34.0)], "cpu_thermal_temp")
            .Should().Be(("lm75_temp", 34.0));

    private static Metric Temperature(string sensor, double value)
        => new() { Group = "thermal_sensors", Name = sensor, IntervalInMinutes = 0, Value = value, Unit = MetricUnit.DegreeCelsius };
}

public sealed class MetricSelection_CorePercents
{
    [Fact]
    public void Should_add_user_system_and_steal_per_core_in_core_order()
        => MetricSelection.CorePercents(
            [
                Cpu("core1", "user", 10), Cpu("core1", "system", 5), Cpu("core1", "idle", 85),
                Cpu("core0", "user", 2), Cpu("core0", "steal", 1), Cpu("total", "user", 6),
            ])
            .Should().Equal(3, 15);

    private static Metric Cpu(string subGroup, string name, double value)
        => new() { Group = "cpu", SubGroup = subGroup, Name = name, IntervalInMinutes = 0, Value = value, Unit = MetricUnit.Percent };
}

public sealed class MetricSelection_NetworkPorts
{
    [Fact]
    public void Should_list_the_sockets_behind_the_interface_with_cable_and_speed()
        => MetricSelection.NetworkPorts(
            [
                Network("lan2", "conduit", "end0"), Network("lan2", "carrier", 0d),
                Network("lan1", "conduit", "end0"), Network("lan1", "carrier", 1d), Network("lan1", "speed", 100d),
                Network("end0", "carrier", 1d), Network("end0", "speed", 1000d),
            ], "end0")
            .Should().Equal(new NetworkPort("lan1", true, 100), new NetworkPort("lan2", false, null));

    [Fact]
    public void Should_find_none_for_an_interface_without_a_switch()
        => MetricSelection.NetworkPorts([Network("wlan0", "carrier", 1d)], "wlan0").Should().BeEmpty();

    private static Metric Network(string networkInterface, string name, object value)
        => new() { Group = "network", SubGroup = networkInterface, Name = name, IntervalInMinutes = 1, Value = value, Unit = MetricUnit.None };
}

public sealed class MetricSelection_CpuClock
{
    [Fact]
    public void Should_report_the_clock_per_core_and_no_limit_while_every_core_may_run_at_its_maximum()
        => MetricSelection.CpuClock(
            [
                Clock("total", "current", 1300), Clock("total", "maximum", 1400),
                Clock("core1", "current", 1200), Clock("core1", "limit", 1400), Clock("core1", "maximum", 1400),
                Clock("core0", "current", 1400), Clock("core0", "limit", 1400), Clock("core0", "maximum", 1400),
            ])
            .Should().BeEquivalentTo(new CpuClock(1300, 1400, null, [1400, 1200]));

    [Fact]
    public void Should_report_the_limit_of_the_most_limited_core()
        => MetricSelection.CpuClock(
            [
                Clock("total", "current", 700), Clock("total", "maximum", 1400),
                Clock("core0", "current", 600), Clock("core0", "limit", 1200), Clock("core0", "maximum", 1400),
                Clock("core1", "current", 800), Clock("core1", "limit", 800), Clock("core1", "maximum", 1400),
            ])!.LimitMhz.Should().Be(800);

    [Fact]
    public void Should_report_nothing_without_cpufreq()
        => MetricSelection.CpuClock([]).Should().BeNull();

    private static Metric Clock(string subGroup, string name, double value)
        => new() { Group = "cpu_frequency", SubGroup = subGroup, Name = name, IntervalInMinutes = 0, Value = value, Unit = MetricUnit.Megahertz };
}

public sealed class MetricSelection_Throttling
{
    [Fact]
    public void Should_take_the_trip_point_of_the_cpu_zone_and_the_strongest_throttling()
        => MetricSelection.Throttling(
            [
                Zone("gpu-thermal", "passive_trip", 90), Zone("gpu-thermal", "throttling", 50),
                Zone("cpu-thermal", "passive_trip", 85), Zone("cpu-thermal", "throttling", 0),
            ])
            .Should().Be((85, 50));

    [Fact]
    public void Should_report_nothing_without_thermal_zones()
        => MetricSelection.Throttling([]).Should().Be((null, null));

    private static Metric Zone(string zone, string name, double value)
        => new() { Group = "thermal_zones", SubGroup = zone, Name = name, IntervalInMinutes = 0, Value = value, Unit = MetricUnit.None };
}
