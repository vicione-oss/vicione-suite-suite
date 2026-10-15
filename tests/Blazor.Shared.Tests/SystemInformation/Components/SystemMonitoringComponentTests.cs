using Blazor.Shared.SystemInformation.Components;
using Blazor.Shared.SystemInformation.Services;
using Bunit;
using Core.Shared.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Sdk.Client.Services;
using ViciOne.SystemMonitoring;
using ViciOne.SystemMonitoring.Metrics;

namespace Blazor.Shared.Tests.SystemInformation.Components;

public sealed class SystemMonitoringComponentTests : IDisposable
{
    private readonly IJsInterop _jsInterop = Substitute.For<IJsInterop>();
    private readonly DailyRangeService _dailyRanges = new(Options.Create(new SystemMonitoringOptions()));
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly DateTimeOffset _now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    public SystemMonitoringComponentTests()
    {
        _timeProvider.LocalTimeZone.Returns(TimeZoneInfo.Utc);
        _timeProvider.GetUtcNow().Returns(_now);
    }

    public void Dispose()
        => _dailyRanges.Dispose();

    [Fact]
    public async Task Should_show_a_dash_without_values()
    {
        await using var ctx = Context();

        var component = ctx.Render<SystemMonitoringComponent>();

        component.FindAll(".gauge text.number").Should().HaveCount(4).And.AllSatisfy(v => v.TextContent.Should().Be("–"));
    }

    [Fact]
    public async Task Should_show_the_latest_minute()
    {
        await using var ctx = Context();
        await _dailyRanges.Publish([Metric(Collectors.Cpu, "total_system", 42.5, "total")], _now.AddMinutes(-1), CancellationToken.None);

        var component = ctx.Render<SystemMonitoringComponent>();

        component.FindAll(".gauge text.number")[0].TextContent.Should().Contain("42");
        component.FindAll("polyline.line.cpu").Should().ContainSingle();
    }

    [Fact]
    public async Task Should_mark_the_time_before_the_first_minute()
    {
        await using var ctx = Context();
        await _dailyRanges.Publish([Metric(Collectors.Cpu, "total_system", 42.5, "total")], _now.AddMinutes(-1), CancellationToken.None);

        var component = ctx.Render<SystemMonitoringComponent>();

        component.FindAll(".no-data").Should().ContainSingle();
        component.FindAll(".no-data-line").Should().Contain(l => l.TextContent.Contains("Suite"));
    }

    [Fact]
    public async Task Should_scale_wifi_throughput_to_a_round_top()
    {
        await using var ctx = Context();
        await _dailyRanges.Publish([Metric(Collectors.Network, "rx_bandwidth", 50_000, "wlan0"), Metric(Collectors.Network, "tx_bandwidth", 50_000, "wlan0")], _now.AddMinutes(-1), CancellationToken.None);

        var component = ctx.Render<SystemMonitoringComponent>();

        await component.FindAll("button.gauge")[3].ClickAsync(new());

        component.Find(".top-label").TextContent.Should().Contain("1").And.Contain("Mbit/s");
    }

    [Fact]
    public async Task Should_switch_to_one_day()
    {
        await using var ctx = Context();
        var component = ctx.Render<SystemMonitoringComponent>();

        await component.FindAll(".span-toggle button")[1].ClickAsync(new());

        component.FindAll(".span-toggle button")[1].GetAttribute("aria-pressed").Should().Be("true");
    }

    [Fact]
    public async Task Should_dispose_js_module_reference()
    {
        var module = Substitute.For<IJSObjectReference>();
        _jsInterop.IncludeModuleScript<SharedClientModule>("system-monitoring-component.js", Arg.Any<CancellationToken>()).Returns(module);

        await using (var ctx = Context())
        {
            ctx.Render<SystemMonitoringComponent>();
        }

        await module.Received(1).DisposeAsync();
    }

    private BunitContext Context()
    {
        var ctx = new BunitContext();
        ctx.Services
            .AddSingleton(_jsInterop)
            .AddSingleton(_dailyRanges)
            .AddKeyedScoped(Sdk.Constants.ClientTimeProviderServiceKey, (_, _) => _timeProvider);
        return ctx;
    }

    private static Metric Metric(string group, string name, double value, string? subGroup = null)
        => new() { Group = group, SubGroup = subGroup, Name = name, IntervalInMinutes = 1, Value = value, Unit = MetricUnit.Percent };
}
