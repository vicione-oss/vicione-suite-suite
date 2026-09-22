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
    /// The <c>Reporting-Endpoints</c> header addressing <see cref="GetBaseline"/>'s
    /// <c>report-to</c> group. Without it the group has no address, so the two are sent together.
    /// </summary>
    public const string ReportingEndpoints = $"{ViolationReportGroup}=\"{CspViolationReporting.Route}\"";

    /// <summary>
    /// The policy of the running suite, as decided in ADR-006.
    /// <paramref name="externalFormActionOrigin"/> is the origin of the configured OpenID provider,
    /// or <see langword="null"/> while none is configured. It is the one value that varies per
    /// installation; there is no development variant.
    /// </summary>
    public static string GetBaseline(string? externalFormActionOrigin) =>
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
        $"form-action {GetFormAction(externalFormActionOrigin)}; " +
        "frame-src 'none'; " +
        "worker-src 'none'; " +
        "object-src 'none'; " +
        "frame-ancestors 'none'; " +
        "upgrade-insecure-requests; " +
        $"report-uri {CspViolationReporting.Route}; " +
        $"report-to {ViolationReportGroup}";

    /// <summary>
    /// The policy of the failsafe and downgrade pages, which their own hosts serve when the suite
    /// did not come up. Same directives as <see cref="GetBaseline"/> with tighter values: neither page
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

    private static string GetFormAction(string? externalOrigin)
        => externalOrigin is null ? "'self'" : $"'self' {externalOrigin}";

    private static string Sha256Source(string content)
        => $"'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(content)))}'";
}
