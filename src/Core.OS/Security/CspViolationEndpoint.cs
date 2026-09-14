using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.OS.Security;

/// <summary>
/// The local violation sink of ADR-006: a same-origin, anonymous endpoint that turns the reports a
/// browser sends about a refused resource into journal entries, so a module the policy breaks shows
/// up in the logs an administrator sends us rather than only in their own browser console. Nothing
/// leaves the customer network.
/// </summary>
internal static partial class CspViolationEndpoint
{
    private const string ViolationReportType = "csp-violation";

    private static readonly JsonSerializerOptions ReportFormat = new(JsonSerializerDefaults.Web);

    public static async Task<IResult> Receive(HttpContext context, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(typeof(CspViolationEndpoint));

        try
        {
            var payload = await JsonSerializer.DeserializeAsync<JsonElement>(
                context.Request.Body, ReportFormat, context.RequestAborted);

            foreach (var violation in ViolationsIn(payload))
                LogViolation(logger, violation.BlockedUri, violation.DocumentUri, violation.Directive);
        }
        catch (JsonException)
        {
            LogUnreadableReport(logger);
            return Results.BadRequest();
        }

        return Results.NoContent();
    }

    /// <summary>
    /// The shape says which of the two reporting directives sent the payload: <c>report-uri</c>
    /// posts a single report wrapped in a <c>csp-report</c> object, <c>report-to</c> posts an array
    /// of them. Reading the shape rather than the content type keeps both working whatever a
    /// browser labels them, and leaves one failure mode instead of two.
    /// </summary>
    private static IEnumerable<CspViolation> ViolationsIn(JsonElement payload)
        => payload.ValueKind == JsonValueKind.Array
            ? ViolationsInBatch(payload)
            : ViolationsInSingleReport(payload);

    private static IEnumerable<CspViolation> ViolationsInBatch(JsonElement payload)
        => payload.Deserialize<IReadOnlyList<Report?>>(ReportFormat)!
            .Where(report => report?.Type == ViolationReportType && report.Body is not null)
            .Select(report => Violation(report!.Body!));

    private static IEnumerable<CspViolation> ViolationsInSingleReport(JsonElement payload)
    {
        var reported = payload.Deserialize<SingleReport>(ReportFormat)?.Violation;

        return reported is null ? [] : [Violation(reported)];
    }

    private static CspViolation Violation(ReportBody body)
        => new(body.BlockedUrl, body.DocumentUrl, body.EffectiveDirective);

    /// <summary>
    /// <c>report-uri</c> predates the split of the two directives, so browsers that only support it
    /// may report the value the policy carries rather than the directive it was checked against.
    /// </summary>
    private static CspViolation Violation(ReportedViolation reported)
        => new(reported.BlockedUri, reported.DocumentUri, reported.EffectiveDirective ?? reported.ViolatedDirective);

    [LoggerMessage(LogLevel.Warning,
        "Content Security Policy refused {BlockedUri} on {DocumentUri}, violating {Directive}")]
    private static partial void LogViolation(ILogger logger, string? blockedUri, string? documentUri, string? directive);

    [LoggerMessage(LogLevel.Warning, "Received a Content Security Policy report that could not be read")]
    private static partial void LogUnreadableReport(ILogger logger);

    private sealed record CspViolation(string? BlockedUri, string? DocumentUri, string? Directive);

    private sealed record SingleReport([property: JsonPropertyName("csp-report")] ReportedViolation? Violation);

    private sealed record ReportedViolation(
        [property: JsonPropertyName("blocked-uri")] string? BlockedUri,
        [property: JsonPropertyName("document-uri")] string? DocumentUri,
        [property: JsonPropertyName("violated-directive")] string? ViolatedDirective,
        [property: JsonPropertyName("effective-directive")] string? EffectiveDirective);

    private sealed record Report(string? Type, ReportBody? Body);

    private sealed record ReportBody(string? BlockedUrl, string? DocumentUrl, string? EffectiveDirective);
}
