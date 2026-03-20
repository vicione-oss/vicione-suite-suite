using System.Runtime.Versioning;
using Blazor.Shared.Logging;
using Blazor.Tests.Tools;
using Bunit;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using ViciOne.Journal;
using Xunit;
using NSubstitute;
using ViciOne.Ui.Blazor.Components.Toolbar.Extensions;

namespace Blazor.Shared.Tests.Logging;

[SupportedOSPlatform("linux")]
public sealed class JournalViewTests
{
    [Fact]
    public async Task Should_render_component()
    {
        // Arrange
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.LocalTimeZone.Returns(TimeZoneInfo.Utc);

        await using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx();
        ctx.Services.AddToolbar();
        ctx.Services.AddSingleton(Substitute.For<IJournalService>());
        ctx.Services.AddKeyedScoped(Sdk.Constants.ClientTimeProviderServiceKey, (_, __) => timeProvider);

        // Act
        var page = ctx.Render<JournalView>();

        // Assert
        page.Should().NotBeNull();
    }
}
