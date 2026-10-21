using System.Globalization;
using Core.OS.E2E.Tests.Infrastructure;
using Core.Tests.Tools;
using Microsoft.Playwright;

namespace Core.OS.E2E.Tests.Security;

/// <summary>
/// Reproduces issue-defect-management#544 against the running suite: a client sending <c>GET /</c> faster than its
/// rate allows is answered with <c>429</c> and <c>Retry-After</c> instead of slowing the device down (suite#2973).
/// </summary>
/// <remarks>
/// The requests name their client in <c>X-Forwarded-For</c>, because a suite started from its launch profile sees
/// this machine as loopback, which is never limited. Behind the packaged nginx the header changes nothing: nginx
/// appends the runner's address and the suite counts the last entry. The requests are then counted against the
/// runner, so the test only ends once the runner is admitted again, before the next test starts.
/// </remarks>
[Collection(E2ECollectionDefinition.Name)]
[Trait(Traits.Category, Traits.E2E)]
public sealed class RateLimitingTests(PlaywrightFixture fixture) : E2ETest(fixture)
{
    private const string RemoteClient = "198.51.100.73";

    /// <summary>
    /// Bounds both loops: far more requests than one client's burst, and far more retries than a refill takes, because
    /// a rejected request takes no permit.
    /// </summary>
    private const int MaxAttempts = 1_000;

    private static readonly APIRequestContextOptions RootWithoutRedirect = new()
    {
        MaxRedirects = 0,
        Headers = new Dictionary<string, string> { ["X-Forwarded-For"] = RemoteClient },
    };

    [Fact]
    public async Task A_client_sending_too_fast_is_answered_with_429_and_admitted_again()
    {
        // One request at a time, so the global cap, which counts requests in progress, is never what rejects them.
        var rejected = await SendUntil(response => response.Status == 429);

        rejected.Should().NotBeNull();
        int.Parse(rejected!.Headers["retry-after"], CultureInfo.InvariantCulture).Should().BePositive();

        var admitted = await SendUntil(response => response.Status != 429);

        admitted.Should().NotBeNull();
    }

    private async Task<IAPIResponse?> SendUntil(Func<IAPIResponse, bool> condition)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var response = await Context.APIRequest.GetAsync("/", RootWithoutRedirect);
            if (condition(response))
                return response;
        }

        return null;
    }
}
