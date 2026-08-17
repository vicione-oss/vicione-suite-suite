using Core.Tests.Tools;
using Microsoft.Playwright;

namespace Core.OS.E2E.Tests.Infrastructure;

/// <summary>
/// Base class for end-to-end tests that drive a master/slave topology. Unlike <see cref="E2ETest"/>
/// (a single instance behind one <c>SUITE_BASE_URL</c>), a master/slave test opens isolated pages
/// against a chosen instance via <see cref="NewPage"/>.
///
/// The instance URLs are resolved from the environment; the defaults match the
/// <c>Master-Ui</c> / <c>Slave1-Ui</c> / <c>Slave2-Ui</c> launch profiles. The CI job is
/// responsible for starting the instances and gating readiness on <c>/hc</c> before the tests run
/// (see docs/e2e-testing.md).
/// </summary>
public abstract class MasterSlaveE2ETest(PlaywrightFixture fixture) : IAsyncLifetime
{
    /// <summary>Default time Playwright waits for actions/assertions, generous for CI cold starts.</summary>
    protected const float DefaultTimeoutMs = 15_000;

    protected static readonly Uri MasterUrl =
        new(IntegrationServiceSettings.GetValue("SUITE_MASTER_URL", "https://localhost:5001"));

    protected static readonly Uri Slave1Url =
        new(IntegrationServiceSettings.GetValue("SUITE_SLAVE1_URL", "https://localhost:6001"));

    protected static readonly Uri Slave2Url =
        new(IntegrationServiceSettings.GetValue("SUITE_SLAVE2_URL", "https://localhost:7001"));

    private readonly List<IBrowserContext> _contexts = [];

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Opens a fresh, isolated page (its own cookies/storage) pointed at a specific
    /// instance, so logging in against one instance never leaks into another. The instance serves
    /// HTTPS with a dev certificate, so TLS errors are ignored.
    /// </summary>
    protected async Task<IPage> NewPage(Uri baseUrl)
    {
        var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = baseUrl.AbsoluteUri,
            IgnoreHTTPSErrors = true
        });
        context.SetDefaultTimeout(DefaultTimeoutMs);

        // Trace every context. On success the trace is discarded; on failure it is saved (see
        // DisposeAsync).
        await PlaywrightTracing.Start(context);
        _contexts.Add(context);

        return await context.NewPageAsync();
    }

    public async ValueTask DisposeAsync()
    {
        // On failure each context's trace is saved to <test-bin>/traces and its path logged; on
        // success the trace is discarded. The per-context index disambiguates the multiple
        // instances a single master/slave test opens.
        var name = PlaywrightTracing.CurrentTestFileName();

        var index = 0;
        foreach (var context in _contexts)
        {
            var tracePath = await PlaywrightTracing.Stop(context, $"{name}-{index++}");

            if (tracePath is not null)
                TestContext.Current.TestOutputHelper?.WriteLine(
                    $"Saved failure trace: {tracePath} (view: playwright show-trace \"{tracePath}\")");

            await context.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
