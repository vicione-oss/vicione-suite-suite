using Core.OS.Security;
using Core.OS.Security.Extensions;
using Core.Shared.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace Core.OS.Tests.Security.Extensions;

/// <summary>
/// Pins what reaches the wire. The policy is emitted by the suite rather than by the packaged
/// nginx (ADR-006), because nginx is absent from Kestrel-direct and customer-proxy deployments.
/// </summary>
public class SecurityHeaderExtensionsTests
{
    private const string ContentSecurityPolicyHeader = "Content-Security-Policy";
    private const string FrameOptionsHeader = "X-Frame-Options";
    private const string ReportingEndpointsHeader = "Reporting-Endpoints";

    /// <summary>What <c>AddInteractiveServerRenderMode</c> emits on a rendered page.</summary>
    private const string FrameworkPolicy = "frame-ancestors 'self'";

    [Fact]
    public async Task Should_send_the_baseline_policy()
    {
        // Arrange
        using var host = await StartHost();

        // Act
        var response = await host.GetTestClient().GetAsync("/", TestContext.Current.CancellationToken);

        // Assert
        response.Headers.GetValues(ContentSecurityPolicyHeader)
            .Should().Equal(ContentSecurityPolicy.Baseline);
    }

    [Fact]
    public async Task Should_keep_the_framework_policy_alongside_the_baseline()
    {
        // Arrange
        using var host = await StartHost(response => response.Headers.ContentSecurityPolicy = FrameworkPolicy);

        // Act
        var response = await host.GetTestClient().GetAsync("/", TestContext.Current.CancellationToken);

        // Assert
        // Both are enforced by the browser, so the effective policy is their intersection.
        response.Headers.GetValues(ContentSecurityPolicyHeader)
            .Should().Equal(FrameworkPolicy, ContentSecurityPolicy.Baseline);
    }

    [Fact]
    public async Task Should_deny_framing_once_even_when_the_header_is_already_set()
    {
        // Arrange
        using var host = await StartHost(response => response.Headers.XFrameOptions = "SAMEORIGIN");

        // Act
        var response = await host.GetTestClient().GetAsync("/", TestContext.Current.CancellationToken);

        // Assert
        // A duplicated X-Frame-Options may be ignored by the browser altogether, so this one is
        // assigned rather than appended.
        response.Headers.GetValues(FrameOptionsHeader).Should().Equal("DENY");
    }

    /// <summary>
    /// The two halves of <c>report-to</c> have to agree: the directive names a group, the header
    /// addresses one. A browser handed a group it cannot resolve delivers nothing, and delivering
    /// nothing is exactly what a working policy looks like from the outside — so the agreement is
    /// asserted here rather than left to be noticed when a violation goes unreported.
    /// </summary>
    [Fact]
    public async Task Should_address_the_reporting_group_the_policy_names()
    {
        // Arrange
        using var host = await StartHost();

        // Act
        var response = await host.GetTestClient().GetAsync("/", TestContext.Current.CancellationToken);

        // Assert
        var group = ReportToGroupOf(response.Headers.GetValues(ContentSecurityPolicyHeader).Single());

        response.Headers.GetValues(ReportingEndpointsHeader)
            .Should().Equal($"{group}=\"{CspViolationReporting.Route}\"");
    }

    /// <summary>
    /// The failsafe and downgrade hosts map no endpoint, so their policy carries no
    /// <c>report-to</c> and their responses must not advertise one either.
    /// </summary>
    [Fact]
    public async Task Should_not_address_a_reporting_group_when_the_host_maps_no_endpoint()
    {
        // Arrange
        using var host = await StartHost(reportingEndpoints: null);

        // Act
        var response = await host.GetTestClient().GetAsync("/", TestContext.Current.CancellationToken);

        // Assert
        response.Headers.Contains(ReportingEndpointsHeader).Should().BeFalse();
    }

    private static string ReportToGroupOf(string policy)
        => policy.Split("; ")
            .Single(directive => directive.StartsWith("report-to ", StringComparison.Ordinal))
            ["report-to ".Length..];

    private static async Task<IHost> StartHost(
        Action<HttpResponse>? beforeResponseStarts = null,
        string? reportingEndpoints = ContentSecurityPolicy.ReportingEndpoints)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();

        var app = builder.Build();
        app.UseSecurityHeaders(ContentSecurityPolicy.Baseline, reportingEndpoints);
        app.MapGet("/", (HttpContext context) => beforeResponseStarts?.Invoke(context.Response));

        await app.StartAsync(TestContext.Current.CancellationToken);

        return app;
    }
}
