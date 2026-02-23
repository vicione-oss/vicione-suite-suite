using System.Runtime.Versioning;
using Blazor.Shared.Logging;
using Blazor.Tests.Tools;
using Bunit;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using ViciOne.Journal;
using Xunit;
using NSubstitute;

namespace Blazor.Shared.Tests.Logging;

[SupportedOSPlatform("linux")]
public sealed class JournalFollowViewTests
{
    [Fact]
    public async Task Should_render_component()
    {
        // Arrange
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.LocalTimeZone.Returns(TimeZoneInfo.Utc);

        await using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx();
        ctx.Services.AddSingleton(Substitute.For<IJournalService>());
        ctx.Services.AddKeyedScoped(Sdk.Constants.ClientTimeProviderServiceKey, (_, __) => timeProvider);

        // Act
        var page = ctx.Render<JournalFollowView>();

        // Assert
        page.Should().NotBeNull();
    }
}
