using AwesomeAssertions.Execution;
using Core.OS.E2E.Tests.Infrastructure;
using Core.OS.E2E.Tests.Pages;
using Core.Tests.Tools;
using Microsoft.Playwright;

namespace Core.OS.E2E.Tests.Security;

/// <summary>
/// Pins the response cache policy: sensitive responses must forbid caching explicitly, and
/// static assets must stay cacheable.
///
/// The suite used to inherit this from ASP.NET Core rather than own it. The framework sets
/// "no-cache, no-store, max-age=0" on rendered pages, but nothing sets it on the JSON
/// endpoints, and on <c>/_blazor/negotiate</c> — which hands out the SignalR connection token —
/// the header appeared only on the request that happened to renew the auth cookie.
/// </summary>
[Collection(E2ECollectionDefinition.Name)]
[Trait(Traits.Category, Traits.E2E)]
public sealed class CacheControlPolicyTests(PlaywrightFixture fixture) : E2ETest(fixture)
{
    private const string ExpectedCacheControl = "no-store, no-cache, must-revalidate";

    /// <summary>
    /// One route per response class the policy covers: an authenticated page, an account page,
    /// the login page the security scan flagged, a JSON endpoint, and a route that matches
    /// nothing — the last one only passes if the policy also covers unmatched requests.
    /// </summary>
    public static TheoryData<string> UncacheableRoutes =>
    [
        "/",
        "/account/change-password",
        LoginPage.Route,
        "/liveness",
        "/no-such-route"
    ];

    /// <summary>Assets that carry no user data and must stay cacheable by the browser.</summary>
    public static TheoryData<string> CacheableAssets =>
    [
        "/_framework/blazor.web.js",
        "/_content/ViciOne.Suite.Blazor.Shared/css/suite.css"
    ];

    [Theory]
    [MemberData(nameof(UncacheableRoutes))]
    public async Task Sensitive_response_forbids_caching(string route)
    {
        // Signed in, so the authenticated routes answer with the real page instead of a redirect.
        await new LoginPage(Page).SignIn(TestUsers.UserName, TestUsers.Password);

        // The API request context shares cookies and base URL with the browser context.
        var response = await Context.APIRequest.GetAsync(route);

        AssertForbidsCaching(response, route);
    }

    [Fact]
    public async Task Signalr_negotiate_forbids_caching_on_every_request()
    {
        await new LoginPage(Page).SignIn(TestUsers.UserName, TestUsers.Password);

        // Repeated because the first call may renew the auth cookie, which sets the headers as a
        // side effect; the policy has to hold on the later calls, where nothing renews.
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var response = await Context.APIRequest.PostAsync("/_blazor/negotiate?negotiateVersion=1");

            AssertForbidsCaching(response, $"/_blazor/negotiate (attempt {attempt})");
        }
    }

    [Theory]
    [MemberData(nameof(CacheableAssets))]
    public async Task Static_asset_stays_cacheable(string asset)
    {
        var response = await Context.APIRequest.GetAsync(asset);

        using var scope = new AssertionScope(asset);

        // Without this the assertion below would also hold for a 404, which has no cache header either.
        response.Ok.Should().BeTrue("the asset must be served, otherwise this guard proves nothing");
        response.Headers.GetValueOrDefault("cache-control", string.Empty)
            .Should().NotContain("no-store", "the asset carries no user data and must stay cacheable");
    }

    private static void AssertForbidsCaching(IAPIResponse response, string route)
    {
        using var scope = new AssertionScope(route);

        response.Headers.Should().Contain("cache-control", ExpectedCacheControl);
        response.Headers.Should().Contain("pragma", "no-cache");
        response.Headers.Should().Contain("expires", "0");
    }
}
