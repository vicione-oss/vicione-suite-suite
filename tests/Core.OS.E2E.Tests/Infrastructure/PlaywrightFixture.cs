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

    public async ValueTask InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (Browser is not null)
            await Browser.DisposeAsync();

        _playwright?.Dispose();
    }
}
