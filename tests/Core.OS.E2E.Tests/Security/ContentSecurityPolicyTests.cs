using AwesomeAssertions.Execution;
using Core.OS.E2E.Tests.Infrastructure;
using Core.OS.E2E.Tests.Pages;
using Core.Tests.Tools;
using Microsoft.Playwright;

namespace Core.OS.E2E.Tests.Security;

/// <summary>
/// Pins the Content Security Policy of ADR-006 as it reaches the browser, and drives the suite
/// under it. The policy is emitted by the suite rather than by the packaged nginx, so it is present
/// in every deployment, and it is enforced rather than report-only — a report-only run would prove
/// only that the browser saw a header, not that the application works with it.
/// </summary>
[Collection(E2ECollectionDefinition.Name)]
[Trait(Traits.Category, Traits.E2E)]
public sealed class ContentSecurityPolicyTests(PlaywrightFixture fixture) : E2ETest(fixture)
{
    private const string ViolationReportRoute = "/csp-report";

    private const string ExpectedPolicy =
        "default-src 'self'; base-uri 'self'; script-src 'self'; script-src-elem 'self'; " +
        "script-src-attr 'none'; style-src 'self'; style-src-attr 'unsafe-inline'; " +
        "img-src 'self'; font-src 'self'; connect-src 'self' wss:; form-action 'self'; " +
        "frame-src 'none'; worker-src 'none'; object-src 'none'; frame-ancestors 'none'; " +
        "upgrade-insecure-requests; report-uri " + ViolationReportRoute + "; report-to csp-endpoint";

    private const string ExpectedReportingEndpoints = "csp-endpoint=\"" + ViolationReportRoute + "\"";

    /// <summary>
    /// A report as Chromium actually sends it through <c>report-to</c>, captured from the wire —
    /// the Reporting API's batch, not the single legacy document.
    /// </summary>
    private const string BrowserViolationReport =
        """
        [{"age":0,"type":"csp-violation","url":"https://localhost:5001/","body":{
            "blockedURL":"https://example.invalid/","documentURL":"https://localhost:5001/",
            "effectiveDirective":"connect-src","disposition":"enforce","statusCode":200}}]
        """;

    private const string DownloadedFileName = "journal.txt";

    private const string OffOriginFetch = "fetch('https://example.invalid/').catch(() => {})";

    /// <summary>
    /// The shipped helper, handed a stand-in for the <c>DotNetStreamReference</c> Blazor passes it —
    /// so the Blob, the <c>blob:</c> URL and the anchor click are the Suite's own code.
    /// </summary>
    private const string DownloadThroughTheSuiteHelper =
        """
        fileName => ViciOne.Download.fileFromStream(fileName, {
            arrayBuffer: () => Promise.resolve(new TextEncoder().encode('entries').buffer)
        })
        """;

    private const string ImageFromABlobUrl =
        """
        () => {
            const image = new Image();
            image.src = URL.createObjectURL(new Blob(['entries']));
            document.body.append(image);
        }
        """;

    /// <summary>
    /// One route per response class the policy has to cover: an authenticated page, an account page
    /// rendered without a circuit, the login page, and a route that matches nothing — the last one
    /// only passes if the error responses the suite produces carry the policy as well.
    /// </summary>
    public static TheoryData<string> PolicyBearingRoutes =>
    [
        "/",
        "/account/change-password",
        LoginPage.Route,
        "/no-such-route"
    ];

    [Theory]
    [MemberData(nameof(PolicyBearingRoutes))]
    public async Task Response_carries_the_enforced_policy(string route)
    {
        // Signed in, so the authenticated routes answer with the real page instead of a redirect.
        await new LoginPage(Page).SignIn(TestUsers.UserName, TestUsers.Password);

        // The API request context shares cookies and base URL with the browser context.
        var response = await Context.APIRequest.GetAsync(route);

        using var scope = new AssertionScope(route);

        ValuesOf(response, "content-security-policy").Should().Contain(ExpectedPolicy);
        ValuesOf(response, "content-security-policy-report-only").Should().BeEmpty();
        ValuesOf(response, "x-frame-options").Should().Equal("DENY");
        ValuesOf(response, "reporting-endpoints").Should().Equal(ExpectedReportingEndpoints);
    }

    /// <summary>
    /// The policy fails closed, so anything it blocks stays broken until a release fixes it. This
    /// walks the paths that would break first — the account pages served as static SSR, the
    /// interactive circuit behind the sign-in, and a virtualized grid — and asserts the browser
    /// refused nothing on the way.
    /// </summary>
    [Fact]
    public async Task Nothing_is_refused_while_signing_in_and_opening_a_grid()
    {
        var policy = CspViolationLog.Watch(Page);

        await new LoginPage(Page).SignIn(TestUsers.UserName, TestUsers.Password);
        await new UsersPanel(Page).Open();

        policy.Refusals.Should().BeEmpty();
    }

    /// <summary>
    /// Pins what <c>connect-src 'self' wss:</c> still closes — an off-origin fetch — and with it
    /// that the check above can fail at all: a refusal the browser stopped reporting the way
    /// <see cref="CspViolationLog"/> recognises would leave that test asserting nothing.
    /// </summary>
    [Fact]
    public async Task An_off_origin_fetch_is_refused_and_recorded()
    {
        var policy = CspViolationLog.Watch(Page);

        await new LoginPage(Page).Goto();

        await Page.RunAndWaitForConsoleMessageAsync(
            () => Page.EvaluateAsync(OffOriginFetch),
            new PageRunAndWaitForConsoleMessageOptions { Predicate = CspViolationLog.IsRefusal });

        policy.Refusals.Should().NotBeEmpty();
    }

    /// <summary>
    /// The download path of ADR-006: every export the Suite offers goes through this helper, which
    /// builds a <c>Blob</c>, makes a <c>blob:</c> URL and clicks an <c>&lt;a download&gt;</c>. No
    /// directive of the baseline names <c>blob:</c>, so the claim that a download is not a fetch
    /// the policy governs has to be shown rather than assumed.
    /// </summary>
    [Fact]
    public async Task A_download_of_a_blob_url_is_not_refused()
    {
        var policy = CspViolationLog.Watch(Page);

        await new LoginPage(Page).SignIn(TestUsers.UserName, TestUsers.Password);

        var download = await Page.RunAndWaitForDownloadAsync(
            () => Page.EvaluateAsync(DownloadThroughTheSuiteHelper, DownloadedFileName));

        download.SuggestedFilename.Should().Be(DownloadedFileName);
        policy.Refusals.Should().BeEmpty();
    }

    /// <summary>
    /// The same <c>blob:</c> URL as a resource, which <c>img-src 'self'</c> refuses. It says the
    /// download above passes because a download is not a fetch the policy governs rather than
    /// because <c>blob:</c> is allowed somewhere, and it is why no export may preview off one.
    /// </summary>
    [Fact]
    public async Task A_blob_url_rendered_as_an_image_is_refused()
    {
        var policy = CspViolationLog.Watch(Page);

        await new LoginPage(Page).Goto();

        await Page.RunAndWaitForConsoleMessageAsync(
            () => Page.EvaluateAsync(ImageFromABlobUrl),
            new PageRunAndWaitForConsoleMessageOptions { Predicate = CspViolationLog.IsRefusal });

        policy.Refusals.Should().ContainMatch("*blob:*");
    }

    /// <summary>
    /// The reporting half of ADR-006: a refusal has to reach the suite, not only the browser
    /// console, because a console is the one place an administrator's report cannot come from.
    /// The browser's own delivery cannot be observed from here — the Reporting API uploads out of
    /// band, from the network service rather than from the page, so it reaches the suite without
    /// ever becoming a request of the page Playwright drives, at any level including CDP. What is
    /// held here is the half that is ours: the running suite advertises the route, and that route
    /// takes the browser's payload shape anonymously, through the real pipeline with its onboarding
    /// redirect and its authentication in place — none of which the integration test exercises.
    /// </summary>
    [Fact]
    public async Task The_advertised_report_endpoint_answers_the_browsers_payload()
    {
        var page = await Context.APIRequest.GetAsync(LoginPage.Route);

        ValuesOf(page, "reporting-endpoints").Should().Equal(ExpectedReportingEndpoints);

        var report = await Context.APIRequest.PostAsync(ViolationReportRoute, new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["content-type"] = "application/reports+json" },
            Data = BrowserViolationReport
        });

        report.Status.Should().Be(204);
    }

    /// <summary>
    /// The other half of an anonymous route: the mapping caps what it will buffer, and only a real
    /// server enforces that cap, so this is the one place the refusal itself can be shown. Without
    /// it the route would take a body of the server's default size — thirty megabytes — from anyone
    /// who can reach the Suite.
    /// </summary>
    [Fact]
    public async Task An_oversized_report_is_refused_by_the_server()
    {
        var response = await Context.APIRequest.PostAsync(ViolationReportRoute, new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["content-type"] = "application/reports+json" },
            Data = new string('x', 128 * 1024)
        });

        response.Status.Should().Be(413);
    }

    private static IEnumerable<string> ValuesOf(IAPIResponse response, string headerName)
        => response.HeadersArray
            .Where(header => header.Name.Equals(headerName, StringComparison.OrdinalIgnoreCase))
            .Select(header => header.Value);
}
