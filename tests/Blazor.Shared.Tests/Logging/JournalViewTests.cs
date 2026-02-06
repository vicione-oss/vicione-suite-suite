using System.Runtime.Versioning;
using Blazor.Shared.Logging;
using Blazor.Tests.Tools;
using Bunit;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using ViciOne.Journal;
using Xunit;

namespace Blazor.Shared.Tests.Logging;

[SupportedOSPlatform("linux")]
public sealed class JournalViewTests
{
    [Fact(Skip = "JournalService can't be abstracted and therefore it's hard to test now")]
    public void Should_render_component()
    {
        // Arrange
        using var ctx = new BunitContext();
        ctx.SetupSuiteServicesWithBlazorDx();
        ctx.Services.AddSingleton<JournalService>();

        // Act
        var page = ctx.Render<JournalView>();

        // Assert
        page.Should().NotBeNull();
    }
}
