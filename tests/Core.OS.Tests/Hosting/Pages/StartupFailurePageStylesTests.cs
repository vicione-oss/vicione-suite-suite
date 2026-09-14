using Core.OS.Hosting.Pages;

namespace Core.OS.Tests.Hosting.Pages;

/// <summary>
/// Both stylesheets are embedded resources, so a renamed or no longer embedded file has to fail
/// here rather than in a host that is only ever built once the suite already did not come up.
/// </summary>
public sealed class StartupFailurePageStylesTests
{
    [Fact]
    public void Should_load_the_failsafe_page_stylesheet()
        => StartupFailurePageStyles.FailsafePage.Should().Contain("body {");

    [Fact]
    public void Should_load_the_downgrade_page_stylesheet()
        => StartupFailurePageStyles.DowngradePage.Should().Contain("body {");
}
