using Core.OS.E2E.Tests.Infrastructure;
using Core.Tests.Tools;
using static Microsoft.Playwright.Assertions;

namespace Core.OS.E2E.Tests.Availability;

/// <summary>
/// Most basic smoke check: the standalone instance is up and serves the UI.
/// </summary>
[Collection(E2ECollectionDefinition.Name)]
[Trait(Traits.Category, Traits.E2E)]
public sealed class AvailabilitySmokeTests(PlaywrightFixture fixture) : E2ETest(fixture)
{
    [Fact]
    public async Task Instance_serves_the_ui()
    {
        // Act
        var response = await Page.GotoAsync("/");

        // Assert
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected a successful response from {BaseUrl}, but got HTTP {response.Status}.");
        await Expect(Page.Locator("body")).ToBeVisibleAsync();
    }
}
