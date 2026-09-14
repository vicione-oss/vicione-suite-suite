using Microsoft.Playwright;

namespace Core.OS.E2E.Tests.Infrastructure;

/// <summary>
/// Shared Playwright instance and browser for all end-to-end tests. The (expensive)
/// browser is launched once per test run; each test gets its own isolated
/// <see cref="IBrowserContext"/> — see <see cref="E2ETest"/>.
/// </summary>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    private IPlaywright _playwright = null!;

    public IBrowser Browser { get; private set; } = null!;

    /// <summary>
    /// The full Chromium build rather than Playwright's default headless shell. The shell never
    /// delivers Reporting API reports, so the Content Security Policy violation that ADR-006 has
    /// reported through <c>report-to</c> would be invisible to the test that asserts it arrives —
    /// and invisible for a reason no customer's browser shares.
    /// </summary>
    private static BrowserTypeLaunchOptions FullChromium => new() { Channel = "chromium" };

    public async ValueTask InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(FullChromium);
    }

    public async ValueTask DisposeAsync()
    {
        if (Browser is not null)
            await Browser.DisposeAsync();

        _playwright?.Dispose();
    }
}
