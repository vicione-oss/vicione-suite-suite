using Microsoft.Playwright;

namespace Core.OS.E2E.Tests.Infrastructure;

/// <summary>
/// Shared Playwright tracing for the E2E base classes: record a trace per browser context, then on
/// test failure save it as a <c>.zip</c> under <c>&lt;test-bin&gt;/traces</c> and on success discard
/// it. View a saved trace with <c>playwright show-trace</c> or by dropping it on
/// https://trace.playwright.dev.
/// </summary>
internal static class PlaywrightTracing
{
    /// <summary>
    /// Starts tracing on a context. Snapshots capture the interactive DOM, so hover-only UI — e.g.
    /// the login form's mouse-over error indicator — is inspectable in the Trace Viewer, unlike a
    /// flat screenshot.
    /// </summary>
    public static Task Start(IBrowserContext context) =>
        context.Tracing.StartAsync(new TracingStartOptions
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });

    /// <summary>
    /// Stops tracing on a context. On failure, saves the trace to
    /// <c>&lt;test-bin&gt;/traces/&lt;fileName&gt;.zip</c> and returns its path; on success, discards
    /// the trace and returns <see langword="null"/>.
    /// </summary>
    public static async Task<string?> Stop(IBrowserContext context, string fileName)
    {
        var failed = TestContext.Current.TestState?.Result == TestResult.Failed;

        string? tracePath = null;
        if (failed)
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "traces");
            Directory.CreateDirectory(directory);
            tracePath = Path.Combine(directory, $"{fileName}.zip");
        }

        await context.Tracing.StopAsync(new TracingStopOptions { Path = tracePath });

        return tracePath;
    }

    /// <summary>The current test's display name sanitized into a usable file name.</summary>
    public static string CurrentTestFileName() =>
        string.Join('_',
            (TestContext.Current.Test?.TestDisplayName ?? "test")
            .Split([.. Path.GetInvalidFileNameChars(), ' '], StringSplitOptions.RemoveEmptyEntries));
}
