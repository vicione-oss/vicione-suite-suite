using System.Security.Cryptography;
using System.Text;
using Core.Shared.Security;

namespace Core.OS.Security;

/// <summary>
/// The Content Security Policies the suite sends, as decided in ADR-006 — one directive per line,
/// in the order of the ADR's baseline table, so header and record can be read against each other.
/// </summary>
internal static class ContentSecurityPolicy
{
    private const string ViolationReportGroup = "csp-endpoint";

    /// <summary>
    /// The policy of the running suite.
    /// </summary>
    /// <remarks>
    /// A constant, not an assembled string: the policy is identical in every environment, and a
    /// development variant would mean E2E proving a header customers never get.
    /// <c>upgrade-insecure-requests</c> has no opt-out because <c>UseHttpsRedirection</c> and
    /// <c>UseHsts</c> already rule out reaching the suite over plain HTTP.
    /// Violations are reported through both reporting directives. <c>report-to</c> is the current
    /// one and names the group <see cref="ReportingEndpoints"/> declares; <c>report-uri</c> is its
    /// predecessor and is what browsers at the floor of ADR-006 still act on. A browser that
    /// supports the successor ignores the predecessor, so no violation is reported twice.
    /// <c>report-to</c> registers an endpoint only on a cryptographic origin: served over plain
    /// <c>http://</c> a browser registers none and still ignores <c>report-uri</c>, so such an
    /// installation reports nothing at all. TLS terminated at a proxy is enough, since what counts
    /// is the scheme the browser sees.
    /// </remarks>
    public const string Baseline =
        "default-src 'self'; " +
        "base-uri 'self'; " +
        "script-src 'self'; " +
        "script-src-elem 'self'; " +
        "script-src-attr 'none'; " +
        "style-src 'self'; " +
        "style-src-attr 'unsafe-inline'; " +
        "img-src 'self'; " +
        "font-src 'self'; " +
        "connect-src 'self' wss:; " +
        "form-action 'self'; " +
        "frame-src 'none'; " +
        "worker-src 'none'; " +
        "object-src 'none'; " +
        "frame-ancestors 'none'; " +
        "upgrade-insecure-requests; " +
        $"report-uri {CspViolationReporting.Route}; " +
        $"report-to {ViolationReportGroup}";

    /// <summary>
    /// The <c>Reporting-Endpoints</c> header that gives <see cref="Baseline"/>'s <c>report-to</c>
    /// group an address. A browser that receives the directive without this header has nowhere to
    /// deliver to, so the two are sent together or not at all.
    /// </summary>
    public const string ReportingEndpoints = $"{ViolationReportGroup}=\"{CspViolationReporting.Route}\"";

    /// <summary>
    /// The policy of the failsafe and downgrade pages, which their own hosts serve when the suite
    /// did not come up. Same directives as <see cref="Baseline"/> with tighter values: neither page
    /// carries a script, and both are laid out entirely by one inline stylesheet, which is allowed
    /// by its hash rather than by <c>'unsafe-inline'</c>.
    /// </summary>
    /// <remarks>
    /// The hash is computed over the very stylesheet the page renders, so it cannot drift from it the
    /// way a hash pasted into a header by hand does.
    /// <c>img-src</c> stays <c>'self'</c> rather than <c>'none'</c> so that the favicon request the
    /// browser makes on its own is not refused: these pages are read to find out what went wrong,
    /// and a violation of our own making in that console is a false lead.
    /// </remarks>
    public static string ForStartupFailurePage(string inlineStyleSheet) =>
        "default-src 'self'; " +
        "base-uri 'self'; " +
        "script-src 'none'; " +
        "script-src-elem 'none'; " +
        "script-src-attr 'none'; " +
        $"style-src {Sha256Source(inlineStyleSheet)}; " +
        "style-src-attr 'none'; " +
        "img-src 'self'; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "form-action 'self'; " +
        "frame-src 'none'; " +
        "worker-src 'none'; " +
        "object-src 'none'; " +
        "frame-ancestors 'none'; " +
        "upgrade-insecure-requests";

    private static string Sha256Source(string content)
        => $"'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(content)))}'";
}
