using Blazor.Shared.SystemInformation.Services;
using Core.Shared.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ViciOne.SystemMonitoring;
using ViciOne.SystemMonitoring.Metrics;

namespace Blazor.Shared.Tests.SystemInformation.Services;

public sealed class ProcessOverviewSampler_Subscribe : IDisposable
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 13, 8, 0, 0, TimeSpan.Zero);

    private readonly ILiveMonitoring _liveMonitoring = Substitute.For<ILiveMonitoring>();
    private readonly List<IDisposable> _liveSubscriptions = [];
    private readonly ServiceProvider _services;
    private readonly ProcessOverviewSampler _sampler;
    private Action<LiveSample>? _onSample;

    public ProcessOverviewSampler_Subscribe()
    {
        _liveMonitoring.Subscribe(Arg.Any<TimeSpan>(), Arg.Do<Action<LiveSample>>(handler => _onSample = handler))
            .Returns(_ =>
            {
                var subscription = Substitute.For<IDisposable>();
                _liveSubscriptions.Add(subscription);
                return subscription;
            });
        _services = new ServiceCollection().AddSingleton(_liveMonitoring).BuildServiceProvider();
        _sampler = new(_services, Options.Create(new SystemMonitoringOptions()));
    }

    public void Dispose()
    {
        _sampler.Dispose();
        _services.Dispose();
    }

    [Fact]
    public void Should_start_sampling_with_the_first_subscriber_only()
    {
        using var first = _sampler.Subscribe();
        using var second = _sampler.Subscribe();

        _liveMonitoring.Received(1).Subscribe(ProcessOverviewSampler.Interval, Arg.Any<Action<LiveSample>>());
    }

    [Fact]
    public void Should_keep_sampling_while_a_subscriber_is_left()
    {
        using var first = _sampler.Subscribe();
        var second = _sampler.Subscribe();

        second.Dispose();
        second.Dispose();

        _liveSubscriptions.Single().DidNotReceive().Dispose();
    }

    [Fact]
    public void Should_stop_sampling_when_the_last_subscriber_leaves()
    {
        var first = _sampler.Subscribe();
        var second = _sampler.Subscribe();

        first.Dispose();
        second.Dispose();

        _liveSubscriptions.Single().Received(1).Dispose();
    }

    [Fact]
    public void Should_ignore_a_sample_that_arrives_after_the_last_subscriber_left()
    {
        _sampler.Subscribe().Dispose();
        var changed = false;
        _sampler.Changed += () => changed = true;

        Sample(30);

        _sampler.Current.Should().BeNull();
        changed.Should().BeFalse();
    }

    [Fact]
    public void Should_start_with_empty_histories_after_a_restart()
    {
        using (_sampler.Subscribe())
        {
            Sample(30);
            Sample(40);
        }

        using var again = _sampler.Subscribe();
        var currentOnRestart = _sampler.Current;
        Sample(50);

        currentOnRestart.Should().BeNull();
        _sampler.Current!.System.Cpu.Samples.Should().Equal(50);
        _liveMonitoring.Received(2).Subscribe(ProcessOverviewSampler.Interval, Arg.Any<Action<LiveSample>>());
    }

    private void Sample(double cpuPercent)
        => _onSample!(new LiveSample(
            [new Metric { Group = Collectors.Cpu, SubGroup = "total", Name = "total_system", IntervalInMinutes = 0, Value = cpuPercent, Unit = MetricUnit.Percent }],
            s_now));
}
