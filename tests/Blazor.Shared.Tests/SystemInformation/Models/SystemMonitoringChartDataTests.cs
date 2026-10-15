using Blazor.Shared.SystemInformation.Models;

namespace Blazor.Shared.Tests.SystemInformation.Models;

public sealed class SystemMonitoringChartData_Create
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_place_a_short_history_at_the_right_edge()
    {
        var data = SystemMonitoringChartData.Create([Sample(-1.5, cpu: 20), Sample(-0.5, cpu: 40)], s_now, day: false);

        data.SlotCount.Should().Be(60);
        data.Values[SystemMonitoringSeries.Cpu][^1].Should().Be(40);
        data.Values[SystemMonitoringSeries.Cpu][^2].Should().Be(20);
        data.Values[SystemMonitoringSeries.Cpu][..^2].Should().AllSatisfy(v => v.Should().BeNull());
    }

    [Fact]
    public void Should_average_the_minutes_of_a_quarter_hour_for_a_day()
    {
        var data = SystemMonitoringChartData.Create([Sample(-14, cpu: 10), Sample(-8, cpu: 30), Sample(-2, cpu: 50)], s_now, day: true);

        data.SlotCount.Should().Be(96);
        data.Values[SystemMonitoringSeries.Cpu][^1].Should().Be(30);
    }

    [Fact]
    public void Should_leave_out_samples_older_than_the_span()
    {
        var data = SystemMonitoringChartData.Create([Sample(-61, cpu: 99), Sample(-1, cpu: 5)], s_now, day: false);

        data.Range(SystemMonitoringSeries.Cpu).Should().Be(new ValueRange(5, 5, 5));
    }

    [Fact]
    public void Should_keep_a_missing_series_empty()
    {
        var data = SystemMonitoringChartData.Create([Sample(-1, cpu: 5)], s_now, day: false);

        data.Range(SystemMonitoringSeries.Network).Should().BeNull();
        data.Latest(SystemMonitoringSeries.Network).Should().BeNull();
    }

    [Fact]
    public void Should_keep_each_slot_at_its_own_value_without_bridging_gaps()
    {
        var data = SystemMonitoringChartData.Create([Sample(-5.5, cpu: 10), Sample(-4.5, cpu: 90), Sample(-1.5, cpu: 10)], s_now, day: false);

        var cpu = data.Values[SystemMonitoringSeries.Cpu];
        cpu[^6].Should().Be(10);
        cpu[^5].Should().Be(90);
        cpu[^4].Should().BeNull();
        cpu[^2].Should().Be(10);
    }

    [Fact]
    public void Should_hold_the_disk_until_its_next_reading()
    {
        var data = SystemMonitoringChartData.Create([Sample(-10.5, cpu: 5, disk: 60), Sample(-5.5, cpu: 5, disk: 61)], s_now, day: false);

        data.Values[SystemMonitoringSeries.Disk][^10..^6].Should().AllSatisfy(v => v.Should().Be(60));
        data.Values[SystemMonitoringSeries.Disk][^1].Should().Be(61);
        data.Values[SystemMonitoringSeries.Disk][..^11].Should().AllSatisfy(v => v.Should().BeNull());
    }

    [Fact]
    public void Should_use_mbit_without_a_link_speed()
    {
        var data = SystemMonitoringChartData.Create([new(s_now.AddMinutes(-1), 5, 50, 60, null, 0.4)], s_now, day: false);

        data.NetworkInMbit.Should().BeTrue();
        data.Latest(SystemMonitoringSeries.Network).Should().Be(0.4);
    }

    [Fact]
    public void Should_hold_a_disk_reading_from_just_before_the_span()
    {
        var data = SystemMonitoringChartData.Create([Sample(-62, cpu: 5, disk: 40), Sample(-57.5, cpu: 5, disk: 41)], s_now, day: false);

        data.Values[SystemMonitoringSeries.Disk][0].Should().Be(40);
        data.Values[SystemMonitoringSeries.Disk][2].Should().Be(41);
    }

    [Fact]
    public void Should_not_bridge_a_long_disk_gap()
    {
        var data = SystemMonitoringChartData.Create([Sample(-30.5, cpu: 5, disk: 40)], s_now, day: false);

        data.Values[SystemMonitoringSeries.Disk][^1].Should().BeNull();
    }

    private static SystemMonitoringSample Sample(double minutesAgo, double cpu, double? disk = 60)
        => new(s_now.AddMinutes(minutesAgo), cpu, 50, disk, null);
}
