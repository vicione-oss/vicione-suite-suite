using Core.Shared.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace Core.OS.Security.Extensions;

internal static class SecurityHeaderExtensions
{
    private const string ReportingEndpointsHeader = "Reporting-Endpoints";

    /// <summary>
    /// What a violation report is allowed to weigh. A single report is a few hundred bytes and the
    /// batches the Reporting API sends a few thousand, so this leaves room for far more than a
    /// browser ever posts at once — while keeping an anonymous route from buffering a body of
    /// Kestrel's default size, thirty megabytes, into memory on someone else's say-so.
    /// </summary>
    private const int MaxViolationReportBytes = 64 * 1024;

    /// <summary>
    /// Sends <paramref name="policy"/> as the Content Security Policy of ADR-006 and the legacy
    /// <c>X-Frame-Options</c> on every dynamic response, so both are present in deployments without
    /// the packaged nginx as well. <paramref name="reportingEndpoints"/> addresses the policy's
    /// <c>report-to</c> group and is omitted by the hosts whose policy has no such group.
    /// </summary>
    /// <remarks>
    /// In the suite, register next to <c>UseResponseCachePolicy</c>, and for its two reasons: static
    /// assets are answered before this runs and carry nothing the policy protects, and the headers
    /// are written from <see cref="HttpResponse.OnStarting(Func{object,Task},object)"/> because the
    /// framework sets a policy of its own while rendering and only a callback registered this early
    /// runs late enough to survive it.
    /// That framework policy — <c>frame-ancestors 'self'</c>, emitted because the interactive server
    /// render mode compresses its WebSocket — is why the policy is concatenated rather than
    /// assigned. A browser enforces every policy it receives, so the two intersect.
    /// <c>X-Frame-Options</c> is assigned, because a duplicate of it may be ignored altogether; the
    /// packaged nginx stops sending it in the release that ships this.
    /// </remarks>
    public static IApplicationBuilder UseSecurityHeaders(
        this IApplicationBuilder app, string policy, string? reportingEndpoints = null)
        => app.UseSecurityHeaders(_ => policy, reportingEndpoints);

    /// <summary>
    /// Like the <c>string</c> overload, but resolves <paramref name="policy"/> once per request.
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(
        this IApplicationBuilder app, Func<HttpContext, string> policy, string? reportingEndpoints = null)
        => app.Use((context, next) =>
        {
            context.Response.OnStarting(static state =>
            {
                var (response, policy, reportingEndpoints) =
                    ((HttpResponse Response, string Policy, string? ReportingEndpoints))state;

                response.Headers.ContentSecurityPolicy =
                    StringValues.Concat(response.Headers.ContentSecurityPolicy, policy);
                response.Headers.XFrameOptions = "DENY";

                if (reportingEndpoints is not null)
                    response.Headers[ReportingEndpointsHeader] = reportingEndpoints;

                return Task.CompletedTask;
            }, (context.Response, policy(context), reportingEndpoints));

            return next(context);
        });

    /// <summary>
    /// Maps the endpoint both of the policy's reporting directives point at, from the class that
    /// sends the policy, so the route a browser is told to post to and the route that answers
    /// cannot drift apart.
    /// </summary>
    public static IEndpointRouteBuilder MapCspViolationReports(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(CspViolationReporting.Route, CspViolationEndpoint.Receive)
            .WithMetadata(new RequestSizeLimitAttribute(MaxViolationReportBytes));

        return endpoints;
    }
}
