using Blazor.Shared.SystemInformation.Models;
using Blazor.Shared.SystemInformation.Services;
using Core.Shared.Monitoring;
using Microsoft.Extensions.Options;
using ViciOne.SystemMonitoring;
using ViciOne.SystemMonitoring.Metrics;

namespace Blazor.Shared.Tests.SystemInformation.Services;

public sealed class DailyRangeService_Publish : IDisposable
{
    private static readonly DateTimeOffset s_start = new(2026, 10, 13, 8, 0, 0, TimeSpan.Zero);

    private readonly ILiveMonitoring _liveMonitoring = Substitute.For<ILiveMonitoring>();
    private readonly IDisposable _liveSubscription = Substitute.For<IDisposable>();
    private readonly DailyRangeService _service;
    private Action<LiveSample>? _onCpuSample;

    public DailyRangeService_Publish()
    {
        _liveMonitoring.Subscribe(Arg.Any<TimeSpan>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Do<Action<LiveSample>>(handler => _onCpuSample = handler))
            .Returns(_liveSubscription);
        _service = new(Options.Create(new SystemMonitoringOptions()), _liveMonitoring);
    }

    public void Dispose()
    {
        _service.Dispose();
        _liveSubscription.Dispose();
    }

    [Fact]
    public async Task Should_merge_the_live_readings_of_the_minute_into_it()
    {
        await _service.Connect(CancellationToken.None);
        LiveCpu(20);
        LiveCpu(70);

        await PublishMinute(0, Cpu(40));

        _service.Minutes().Single().Should().Match<MinuteLoad>(m => m.Cpu == 40 && m.CpuMin == 20 && m.CpuMax == 70);
    }

    [Fact]
    public async Task Should_widen_the_live_range_by_the_minute_itself()
    {
        await _service.Connect(CancellationToken.None);
        LiveCpu(30);
        LiveCpu(50);

        await PublishMinute(0, Cpu(60));

        _service.Minutes().Single().CpuMax.Should().Be(60);
    }

    [Fact]
    public async Task Should_start_the_next_minute_without_the_live_readings_of_the_last()
    {
        await _service.Connect(CancellationToken.None);
        LiveCpu(20);
        await PublishMinute(0, Cpu(40));

        await PublishMinute(1, Cpu(45));

        _service.Minutes()[1].Should().Match<MinuteLoad>(m => m.CpuMin == null && m.CpuMax == null);
    }

    [Fact]
    public async Task Should_keep_the_minutes_of_one_day()
    {
        for (var minute = 0; minute <= 24 * 60; minute++)
            await PublishMinute(minute, Cpu(10));

        var minutes = _service.Minutes();
        minutes.Should().HaveCount(24 * 60);
        minutes[0].Time.Should().Be(s_start.AddMinutes(1), "the first minute fell out of the day");
    }

    [Fact]
    public async Task Should_add_up_the_bytes_written_by_all_disks_over_the_day()
    {
        await PublishMinute(0, Written("sda", 100), Written("sdb", 50));
        await PublishMinute(1, Written("sda", 200));

        _service.Current.WrittenBytes.Should().Be(350);
    }

    [Fact]
    public async Task Should_leave_the_minutes_of_the_previous_day_out_of_the_sums()
    {
        await PublishMinute(0, Written("sda", 1_000));
        for (var minute = 1; minute <= 24 * 60; minute++)
            await PublishMinute(minute, Written("sda", 1));

        _service.Current.WrittenBytes.Should().Be(24 * 60);
    }

    [Fact]
    public async Task Should_stop_the_live_readings_on_disconnect()
    {
        await _service.Connect(CancellationToken.None);

        await _service.Disconnect(CancellationToken.None);

        _liveSubscription.Received(1).Dispose();
    }

    private void LiveCpu(double percent)
        => _onCpuSample!(new LiveSample([Cpu(percent)], s_start));

    private Task PublishMinute(int minute, params Metric[] metrics)
        => _service.Publish(metrics, s_start.AddMinutes(minute), CancellationToken.None);

    private static Metric Cpu(double percent)
        => new() { Group = Collectors.Cpu, SubGroup = "total", Name = "total_system", IntervalInMinutes = 1, Value = percent, Unit = MetricUnit.Percent };

    private static Metric Written(string disk, double bytes)
        => new() { Group = Collectors.DiskIo, SubGroup = disk, Name = "written_bytes", IntervalInMinutes = 1, Value = bytes, Unit = MetricUnit.None };
}
