using System.Net;
using Core.OS.Extensions;
using Core.OS.Instance.Extensions;
using Core.OS.Security;
using Core.OS.Security.Extensions;
using Core.Shared.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.OS.Tests.Security;

/// <summary>
/// Pins the limits of suite#2973 on a host that orders its middleware like <c>RunCoreOs</c>: forwarded headers,
/// routing, then the rate limiter.
/// </summary>
public class RequestRateLimitingTests
{
    private const string Client = "192.0.2.10";
    private const string OtherClient = "192.0.2.20";
    private const string SignInRoute = "/account/login";

    public sealed class PerClient : RequestRateLimitingTests
    {
        [Fact]
        public async Task Should_reject_a_client_with_429_and_retry_after_once_its_burst_is_used()
        {
            // Arrange
            await using var host = await StartHost(SmallBurst);
            await Send(host, Client);
            await Send(host, Client);

            // Act
            var rejected = await Send(host, Client);

            // Assert
            rejected.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
            rejected.Response.Headers.RetryAfter.ToString().Should().Be("1");
        }

        [Fact]
        public async Task Should_count_each_client_address_separately()
        {
            // Arrange
            await using var host = await StartHost(SmallBurst);
            await Send(host, Client);
            await Send(host, Client);

            // Act
            var other = await Send(host, OtherClient);

            // Assert
            other.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        }

        [Fact]
        public async Task Should_count_the_forwarded_client_behind_a_loopback_proxy()
        {
            // Arrange
            await using var host = await StartHost(SmallBurst);
            await Send(host, "127.0.0.1", forwardedFor: Client);
            await Send(host, "127.0.0.1", forwardedFor: Client);

            // Act
            var limited = await Send(host, "127.0.0.1", forwardedFor: Client);
            var other = await Send(host, "127.0.0.1", forwardedFor: OtherClient);

            // Assert
            limited.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
            other.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        }

        [Fact]
        public async Task Should_count_an_ipv4_mapped_address_as_its_ipv4_client()
        {
            // Arrange
            await using var host = await StartHost(SmallBurst);
            await Send(host, Client);
            await Send(host, Client);

            // Act
            var mapped = await Send(host, $"::ffff:{Client}");

            // Assert
            mapped.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        }

        [Fact]
        public async Task Should_log_the_first_rejection_of_a_client_only_even_when_rejections_arrive_at_once()
        {
            // Arrange
            using var logger = new RecordingLoggerFactory();
            await using var host = await StartHost(SmallBurst, logger);

            // Act
            await Task.WhenAll(Enumerable.Range(0, 50).SelectMany(_ => new[] { Send(host, Client), Send(host, OtherClient) }));

            // Assert
            logger.Warnings.Should().HaveCount(2);
            logger.Warnings.Should().ContainSingle(warning => warning.Contains(Client, StringComparison.Ordinal));
            logger.Warnings.Should().ContainSingle(warning => warning.Contains(OtherClient, StringComparison.Ordinal));
        }
    }

    public sealed class SignIn : RequestRateLimitingTests
    {
        [Fact]
        public async Task Should_reject_sign_in_posts_beyond_the_limit_per_minute()
        {
            // Arrange
            await using var host = await StartHost(TwoSignInsPerMinute);
            await Send(host, Client, SignInRoute, HttpMethods.Post);
            await Send(host, Client, SignInRoute, HttpMethods.Post);

            // Act
            var rejected = await Send(host, Client, SignInRoute, HttpMethods.Post);
            var other = await Send(host, OtherClient, SignInRoute, HttpMethods.Post);

            // Assert
            rejected.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
            rejected.Response.Headers.RetryAfter.ToString().Should().Be("30");
            other.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        }

        [Fact]
        public async Task Should_not_count_loading_the_sign_in_page()
        {
            // Arrange
            await using var host = await StartHost(TwoSignInsPerMinute);
            for (var i = 0; i < 5; i++)
                await Send(host, Client, SignInRoute);

            // Act
            var signIn = await Send(host, Client, SignInRoute, HttpMethods.Post);

            // Assert
            signIn.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        }
    }

    public sealed class GlobalCap : RequestRateLimitingTests
    {
        [Fact]
        public async Task Should_reject_requests_beyond_the_cap_right_away_and_admit_them_again_once_one_completes()
        {
            // Arrange
            using var logger = new RecordingLoggerFactory();
            using var entered = new SemaphoreSlim(0);
            var release = new TaskCompletionSource();
            await using var host = await StartHost(
                new() { ["RateLimiting:MaxConcurrentRequests"] = "2" },
                logger,
                app => app.MapGet("/held", async () =>
                {
                    entered.Release();
                    await release.Task;
                }));

            var held = new[] { Send(host, "192.0.2.1", "/held"), Send(host, "192.0.2.2", "/held") };
            await entered.WaitAsync(TestContext.Current.CancellationToken);
            await entered.WaitAsync(TestContext.Current.CancellationToken);

            // Act
            var rejected = await Send(host, "192.0.2.3");
            release.SetResult();
            await Task.WhenAll(held);
            var admitted = await Send(host, "192.0.2.3");

            // Assert
            rejected.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
            rejected.Response.Headers.RetryAfter.ToString().Should().Be("1");
            admitted.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
            logger.Warnings.Should().ContainSingle(warning => warning.Contains("2 requests are in progress", StringComparison.Ordinal));
        }
    }

    public sealed class Exemptions : RequestRateLimitingTests
    {
        [Theory]
        [InlineData("GET", "/_blazor/negotiate")]
        [InlineData("GET", "/_framework/blazor.web.js")]
        [InlineData("GET", "/hc")]
        [InlineData("GET", "/liveness")]
        [InlineData("POST", "/csp-report")]
        public async Task Should_never_limit_circuits_health_checks_and_csp_reports(string method, string path)
        {
            // Arrange
            await using var host = await StartHost(SmallBurst);
            await Send(host, Client);
            await Send(host, Client);

            // Act
            var response = await Send(host, Client, path, method);

            // Assert
            response.Response.StatusCode.Should().NotBe(StatusCodes.Status429TooManyRequests);
        }

        [Theory]
        [InlineData("127.0.0.1")]
        [InlineData("::1")]
        [InlineData("::ffff:127.0.0.1")]
        public async Task Should_never_limit_loopback_clients(string loopback)
        {
            // Arrange
            await using var host = await StartHost(SmallBurst);
            await Send(host, loopback);
            await Send(host, loopback);

            // Act
            var response = await Send(host, loopback);

            // Assert
            response.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        }

        [Fact]
        public async Task Should_not_limit_anything_when_disabled()
        {
            // Arrange
            await using var host = await StartHost(new(SmallBurst)
            {
                ["RateLimiting:Enabled"] = "false",
                ["RateLimiting:SignInPermitsPerMinute"] = "1",
            });
            for (var i = 0; i < 3; i++)
            {
                await Send(host, Client);
                await Send(host, Client, SignInRoute, HttpMethods.Post);
            }

            // Act
            var page = await Send(host, Client);
            var signIn = await Send(host, Client, SignInRoute, HttpMethods.Post);

            // Assert
            page.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
            signIn.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        }
    }

    public sealed class Options : RequestRateLimitingTests
    {
        [Theory]
        [InlineData(nameof(RateLimitingOptions.PermitsPerSecond))]
        [InlineData(nameof(RateLimitingOptions.Burst))]
        [InlineData(nameof(RateLimitingOptions.SignInPermitsPerMinute))]
        [InlineData(nameof(RateLimitingOptions.MaxConcurrentRequests))]
        public void Should_fail_startup_when_a_limit_is_zero(string setting)
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { [$"RateLimiting:{setting}"] = "0" });
            builder.Services.AddRequestRateLimiting();
            using var host = builder.Build();

            // Act
            var failures = host.GetInvalidOptions();

            // Assert
            failures.Should().ContainSingle(failure => failure.Contains(setting, StringComparison.Ordinal));
        }
    }

    /// <summary>A burst of two, refilled at one request per second.</summary>
    private static Dictionary<string, string?> SmallBurst => new()
    {
        ["RateLimiting:Burst"] = "2",
        ["RateLimiting:PermitsPerSecond"] = "1",
    };

    private static Dictionary<string, string?> TwoSignInsPerMinute => new()
    {
        ["RateLimiting:SignInPermitsPerMinute"] = "2",
    };

    private static Task<HttpContext> Send(WebApplication host, string client, string path = "/", string method = "GET",
        string? forwardedFor = null)
        => host.GetTestServer().SendAsync(context =>
        {
            context.Request.Method = method;
            context.Request.Path = path;
            context.Connection.RemoteIpAddress = IPAddress.Parse(client);

            if (forwardedFor is not null)
                context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        }, TestContext.Current.CancellationToken);

    private static async Task<WebApplication> StartHost(Dictionary<string, string?> settings,
        ILoggerFactory? loggerFactory = null, Action<WebApplication>? map = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddMemoryCache();
        builder.Services.AddHealthChecks();
        builder.Services.AddRequestRateLimiting();
        builder.Services.Configure<ForwardedHeadersOptions>(options => options.ForwardedHeaders = ForwardedHeaders.XForwardedFor);

        if (loggerFactory is not null)
            builder.Services.AddSingleton(loggerFactory);

        var app = builder.Build();
        app.UseForwardedHeaders();
        app.UseRouting();
        app.UseRateLimiter();

        app.MapGet("/", () => "page");
        app.MapGet(SignInRoute, () => "sign-in page").RequireRateLimiting(RateLimitPolicies.SignIn);
        app.MapPost(SignInRoute, () => "signed in").RequireRateLimiting(RateLimitPolicies.SignIn);
        app.MapGet("/_blazor/negotiate", () => "circuit");
        app.MapGet("/_framework/blazor.web.js", () => "script");
        app.MapHealthEndpoints();
        app.MapCspViolationReports();
        map?.Invoke(app);

        await app.StartAsync(TestContext.Current.CancellationToken);

        return app;
    }

    /// <summary>
    /// Records the warnings of <see cref="RequestRateLimiting"/> only, so a framework warning cannot change a count.
    /// </summary>
    private sealed class RecordingLoggerFactory : ILoggerFactory
    {
        public List<string> Warnings { get; } = [];

        public ILogger CreateLogger(string categoryName)
            => categoryName == typeof(RequestRateLimiting).FullName ? new Recorder(Warnings) : NullLogger.Instance;

        public void AddProvider(ILoggerProvider provider) { }

        public void Dispose() { }

        private sealed class Recorder(List<string> warnings) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (logLevel != LogLevel.Warning)
                    return;

                // Concurrent requests log from several threads.
                lock (warnings)
                    warnings.Add(formatter(state, exception));
            }
        }
    }
}
