using Core.Tests.Tools;
using Microsoft.Playwright;
using Xunit;

namespace Core.OS.E2E.Tests.Infrastructure;

/// <summary>
/// Base class for end-to-end UI smoke tests. Gives each test an isolated browser
/// <see cref="Page"/> (its own cookies/storage), pointed at the Suite instance under
/// test (<c>SUITE_BASE_URL</c>, default <c>https://localhost:5001</c>).
///
/// To add a smoke test, create a class like:
/// <code>
/// using Core.OS.E2E.Tests.Infrastructure;
/// using Core.Tests.Tools;
///
/// [Collection(E2ECollectionDefinition.Name)]
/// [Trait(Traits.Category, Traits.E2E)]
/// public sealed class MyFeatureSmokeTests(PlaywrightFixture fixture) : E2ETest(fixture)
/// {
///     [Fact]
///     public async Task Does_the_thing() { /* drive Page, ideally via a page object */ }
/// }
/// </code>
/// Prefer page objects under <c>Pages/</c> over raw selectors so tests stay readable.
/// </summary>
public abstract class E2ETest(PlaywrightFixture fixture) : IAsyncLifetime
{
    /// <summary>Default time Playwright waits for actions/assertions, generous for CI cold starts.</summary>
    protected const float DefaultTimeoutMs = 15_000;

    protected static readonly string BaseUrl =
        IntegrationServiceSettings.GetValue("SUITE_BASE_URL", "https://localhost:5001");

    private IBrowserContext _context = null!;

    /// <summary>The browser context backing <see cref="Page"/> (its own cookies/storage).</summary>
    protected IBrowserContext Context => _context;

    /// <summary>A fresh, isolated page for the current test.</summary>
    protected IPage Page { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        // A fresh context per test means authentication in one test never leaks into
        // another. The instance serves HTTPS with a dev certificate, so TLS is ignored.
        _context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseUrl,
            IgnoreHTTPSErrors = true
        });
        _context.SetDefaultTimeout(DefaultTimeoutMs);

        // Trace the context. On success the trace is discarded; on failure it is saved (see
        // DisposeAsync).
        await PlaywrightTracing.Start(_context);

        Page = await _context.NewPageAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_context is not null)
        {
            var tracePath = await PlaywrightTracing.Stop(_context, PlaywrightTracing.CurrentTestFileName());
            if (tracePath is not null)
                TestContext.Current.TestOutputHelper?.WriteLine(
                    $"Saved failure trace: {tracePath} (view: playwright show-trace \"{tracePath}\")");

            await _context.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
