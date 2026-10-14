using Blazor.Shared.SystemInformation.Models;
using Blazor.Shared.SystemInformation.Services;

namespace Blazor.Shared.Tests.SystemInformation.Services;

public sealed class LoadPeaksTests
{
    private static readonly DateTimeOffset s_start = new(2026, 10, 13, 6, 0, 0, TimeSpan.Zero);
    private static readonly TopProcess s_engineHost = new(1642, "dotnet", SuiteProcessRole.EngineHost, 61, 418_000);

    [Fact]
    public void Should_find_a_stretch_above_the_threshold_with_its_highest_minute()
    {
        var peak = LoadPeaks.Find(Minutes(cpu: [30, 85, 92, 88, 30])).Should().ContainSingle().Subject;

        peak.Kind.Should().Be(PeakKind.Cpu);
        peak.Start.Should().Be(s_start.AddMinutes(1));
        peak.Minutes.Should().Be(3);
        peak.PeakTime.Should().Be(s_start.AddMinutes(2));
        peak.PeakValue.Should().Be(92);
        peak.TopProcesses.Should().Equal(s_engineHost);
        peak.Course.Should().HaveCount(5);
    }

    [Fact]
    public void Should_split_peaks_at_a_minute_below_the_threshold_and_list_the_newest_first()
    {
        var peaks = LoadPeaks.Find(Minutes(cpu: [85, 30, 90]));

        peaks.Select(p => p.PeakValue).Should().Equal(90, 85);
    }

    [Fact]
    public void Should_note_throttling_and_find_temperature_and_memory_pressure()
    {
        var minutes = Minutes(cpu: [10, 10]);
        minutes[1] = minutes[1] with { Temperature = 86, Refaults = 180, ThrottlingPercent = 100 };

        var peaks = LoadPeaks.Find(minutes);

        peaks.Should().Contain(p => p.Kind == PeakKind.Temperature && p.Throttled);
        peaks.Should().Contain(p => p.Kind == PeakKind.MemoryPressure && p.TopProcesses.Count == 0);
    }

    [Fact]
    public void Should_find_nothing_below_the_thresholds()
        => LoadPeaks.Find(Minutes(cpu: [10, 20, 79])).Should().BeEmpty();

    private static List<MinuteLoad> Minutes(double[] cpu)
        => [.. cpu.Select((value, i) => new MinuteLoad(s_start.AddMinutes(i), value, 50, 0, 0, [s_engineHost], []))];
}
