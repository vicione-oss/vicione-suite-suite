using Blazor.Shared.SystemInformation.Components.ProcessOverview;
using Blazor.Shared.SystemInformation.Models;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Services;
using ViciOne.Ui.Blazor.Components.Dialog.Components;
using ViciOne.Ui.Blazor.Components.Dialog.Extensions;
using ViciOne.Ui.Blazor.Components.TestingHelpers.Dialog.Extensions;

namespace Blazor.Shared.Tests.SystemInformation.Components;

public sealed class PeakDialogTests
{
    private static readonly DateTimeOffset s_peakTime = new(2026, 10, 13, 10, 53, 0, TimeSpan.Zero);

    [Fact]
    public async Task Should_show_the_course_of_the_peak()
    {
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();
        ctx.Services.AddSingleton(Substitute.For<IJsInterop>());
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.LocalTimeZone.Returns(TimeZoneInfo.Utc);
        ctx.Services.AddKeyedSingleton(Sdk.Constants.ClientTimeProviderServiceKey, timeProvider);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var course = Enumerable.Range(-10, 21).Select(i => (s_peakTime.AddMinutes(i), (double?)(i == 0 ? 99 : 20))).ToArray();
        var peak = new LoadPeak(PeakKind.Cpu, s_peakTime, 1, s_peakTime, 99, 80, false, [], course);

        var component = ctx.Render<PeakDialog>(parameters => parameters
            .Add(p => p.Peak, peak)
            .Add(p => p.Visible, true));
        var body = component.FindComponent<Dialog>().RenderSectionContent(ctx);

        body.FindAll(".load-chart polyline.line.cpu").Should().ContainSingle();
        body.FindAll(".load-chart .highlight").Should().ContainSingle();
    }
}
