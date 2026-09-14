using System.Text;
using Core.OS.Security;
using Core.OS.Security.Extensions;
using Core.Shared.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Core.OS.Tests.Security;

/// <summary>
/// The local violation sink of ADR-006. What matters is that an incident reaches the journal, so
/// every fact here reads the log line rather than the response: the log is what an administrator
/// sends us when a module stops working under the policy.
/// </summary>
public class CspViolationEndpointTests
{
    private const string BlockedUri = "https://cdn.example.invalid/module.js";
    private const string DocumentUri = "https://suite.example.invalid/";

    private static readonly string SingleReport =
        $$"""
        {
            "csp-report": {
                "document-uri": "{{DocumentUri}}",
                "blocked-uri": "{{BlockedUri}}",
                "violated-directive": "script-src-elem 'self'",
                "effective-directive": "script-src-elem"
            }
        }
        """;

    private static readonly string ReportBatch =
        $$"""
        [
            {
                "type": "csp-violation",
                "body": {
                    "documentURL": "{{DocumentUri}}",
                    "blockedURL": "{{BlockedUri}}",
                    "effectiveDirective": "script-src-elem"
                }
            },
            {
                "type": "deprecation",
                "body": { "id": "AnythingElseTheGroupCollects" }
            }
        ]
        """;

    /// <summary>
    /// The shape <c>report-uri</c> posts, and the one the browser floor of ADR-006 acts on.
    /// </summary>
    [Fact]
    public async Task Should_log_a_single_report()
    {
        // Arrange
        using var logger = new RecordingLoggerFactory();

        // Act
        await CspViolationEndpoint.Receive(RequestOf(SingleReport), logger);

        // Assert
        logger.Warnings.Should().ContainSingle()
            .Which.Should().Be($"Content Security Policy refused {BlockedUri} on {DocumentUri}, violating script-src-elem");
    }

    /// <summary>
    /// The shape <c>report-to</c> posts: a batch, and one a group may collect other report types
    /// into, which is why the endpoint reads the type rather than assuming every entry is a
    /// violation.
    /// </summary>
    [Fact]
    public async Task Should_log_every_violation_in_a_batch_and_nothing_else()
    {
        // Arrange
        using var logger = new RecordingLoggerFactory();

        // Act
        await CspViolationEndpoint.Receive(RequestOf(ReportBatch), logger);

        // Assert
        logger.Warnings.Should().ContainSingle()
            .Which.Should().Be($"Content Security Policy refused {BlockedUri} on {DocumentUri}, violating script-src-elem");
    }

    /// <summary>
    /// The endpoint is anonymous, so anything at all may be posted to it. A body that is not a
    /// report has to be answered, not thrown on.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("""{ "csp-report": 42 }""")]
    public async Task Should_reject_a_body_that_is_not_a_report(string body)
    {
        // Arrange
        using var logger = new RecordingLoggerFactory();

        // Act
        var result = await CspViolationEndpoint.Receive(RequestOf(body), logger);

        // Assert
        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.BadRequest>();
        logger.Warnings.Should().ContainSingle()
            .Which.Should().Be("Received a Content Security Policy report that could not be read");
    }

    /// <summary>
    /// The route is anonymous, so the mapping caps what it is willing to buffer rather than taking
    /// whatever the server's default allows. Only a real server enforces that cap — a
    /// <c>TestServer</c> ignores it — so what is pinned here is the declaration, whose removal is
    /// the regression; the refusal itself is asserted end to end against Kestrel.
    /// </summary>
    [Fact]
    public async Task Should_declare_a_body_limit_far_below_the_server_default()
    {
        // Arrange
        const long kestrelDefaultBytes = 30_000_000;
        using var host = await StartHost();

        // Act
        var declared = ReportEndpointOf(host).Metadata
            .GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize;

        // Assert
        declared.Should().NotBeNull().And.BeLessThan(kestrelDefaultBytes);
    }

    private static Endpoint ReportEndpointOf(IHost host)
        => host.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Single(endpoint => endpoint is RouteEndpoint route
                && route.RoutePattern.RawText == CspViolationReporting.Route);

    private static async Task<IHost> StartHost()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();

        var app = builder.Build();
        app.MapCspViolationReports();

        await app.StartAsync(TestContext.Current.CancellationToken);

        return app;
    }

    private static HttpContext RequestOf(string body)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));

        return context;
    }

    private sealed class RecordingLoggerFactory : ILoggerFactory, ILogger
    {
        public List<string> Warnings { get; } = [];

        public ILogger CreateLogger(string categoryName) => this;

        public void AddProvider(ILoggerProvider provider) { }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
                Warnings.Add(formatter(state, exception));
        }

        public void Dispose() { }
    }
}
