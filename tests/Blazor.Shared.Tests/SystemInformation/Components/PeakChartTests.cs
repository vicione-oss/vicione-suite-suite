using Blazor.Shared.SystemInformation.Components.ProcessOverview;

namespace Blazor.Shared.Tests.SystemInformation.Components;

public sealed class PeakChart_Span
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(4, 60)]
    [InlineData(70, 70)]
    [InlineData(600, 600)]
    [InlineData(3000, 1440)]
    public void Should_span_the_time_since_the_start_between_an_hour_and_a_day(int runningMinutes, int spanMinutes)
        => PeakChart.Span(s_now.AddMinutes(-runningMinutes), s_now).Should().Be(TimeSpan.FromMinutes(spanMinutes));
}
