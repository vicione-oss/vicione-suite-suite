using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using Blazor.Server.Backend.UserManagement;
using Blazor.Shared;
using Core.Shared.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Blazor.Server.Tests.UserManagement;

public class ExternalLoginFailureRedirectTests
{
    private const string Authority = "https://provider.invalid";
    private const string ChallengeRoute = "/challenge";
    private const string CallbackRoute = "/signin-oidc";

    [Fact]
    public async Task Should_send_a_cancelled_sign_in_back_to_the_login_page()
    {
        // Arrange
        using var host = await StartHost();
        var client = host.GetTestClient();

        // Act
        using var response = await Callback(client, "access_denied");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location?.OriginalString.Should().Be(IdentityRoutes.LoginRoute);
    }

    [Fact]
    public async Task Should_tell_the_login_page_that_the_sign_in_was_cancelled()
    {
        // Arrange
        using var host = await StartHost();
        var client = host.GetTestClient();

        // Act
        using var response = await Callback(client, "access_denied");

        // Assert
        var message = await FollowRedirect(client, response);
        message.Should().Be(((int)ExternalLoginError.SignInCancelled).ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Should_send_any_other_provider_failure_back_to_the_login_page()
    {
        // Arrange
        using var host = await StartHost();
        var client = host.GetTestClient();

        // Act
        using var response = await Callback(client, "server_error");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location?.OriginalString.Should().Be(IdentityRoutes.LoginRoute);
        (await FollowRedirect(client, response))
            .Should().Be(((int)ExternalLoginError.LoginFailed).ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Should_log_a_failed_correlation_as_a_warning()
    {
        // Arrange
        using var logs = new RecordingLoggerProvider();
        using var host = await StartHost(logs);
        var client = host.GetTestClient();

        // Act
        using var response = await Callback(client, "server_error", replayCorrelationCookie: false);

        // Assert
        logs.Messages(typeof(ExternalLoginFailureRedirect).FullName!, LogLevel.Warning)
            .Should().ContainSingle()
            .Which.Should().Contain("Correlation failed");
    }

    /// <summary>
    /// Replays the challenge's state and correlation cookie, which the handler checks before the error.
    /// </summary>
    private static async Task<HttpResponseMessage> Callback(HttpClient client, string error,
        bool replayCorrelationCookie = true)
    {
        using var challenge = await client.GetAsync(ChallengeRoute, TestContext.Current.CancellationToken);

        var state = QueryHelpers.ParseQuery(challenge.Headers.Location?.Query)["state"].ToString();
        var route = QueryHelpers.AddQueryString(CallbackRoute, new Dictionary<string, string?>
        {
            ["error"] = error,
            ["state"] = state
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        if (replayCorrelationCookie)
            request.Headers.Add("Cookie", CookiesOf(challenge));

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Carries the cookies along, because the message travels in one of its own.
    /// </summary>
    private static async Task<string> FollowRedirect(HttpClient client, HttpResponseMessage response)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, response.Headers.Location);
        request.Headers.Add("Cookie", CookiesOf(response));

        using var page = await client.SendAsync(request, TestContext.Current.CancellationToken);

        return await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private static string CookiesOf(HttpResponseMessage response)
        => response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? string.Join("; ", cookies.Select(cookie => cookie.Split(';')[0]))
            : string.Empty;

    private static async Task<IHost> StartHost(ILoggerProvider? loggerProvider = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();

        if (loggerProvider is not null)
            builder.Logging.AddProvider(loggerProvider);

        builder.Services.AddRazorPages();
        builder.Services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie()
            .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.Authority = Authority;
                options.ClientId = "suite";
                options.CallbackPath = CallbackRoute;
                options.Configuration = new OpenIdConnectConfiguration
                {
                    Issuer = Authority,
                    AuthorizationEndpoint = $"{Authority}/oauth/authorize",
                    TokenEndpoint = $"{Authority}/oauth/token"
                };
            });
        builder.Services.ConfigureOptions<ExternalLoginFailureRedirect>();

        var app = builder.Build();
        app.UseAuthentication();

        app.MapGet(ChallengeRoute, () => Results.Challenge(
            new AuthenticationProperties { RedirectUri = "/" },
            [OpenIdConnectDefaults.AuthenticationScheme]));

        app.MapGet(IdentityRoutes.LoginRoute, (HttpContext context, ITempDataDictionaryFactory tempData) =>
            (tempData.GetTempData(context)[ExternalLoginErrorConstants.ExternalLoginErrorKey] as int?)
            ?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);

        await app.StartAsync(TestContext.Current.CancellationToken);

        return app;
    }

    private sealed record LogEntry(string Category, LogLevel Level, string Message);

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();

        public IEnumerable<string> Messages(string category, LogLevel level)
            => _entries.Where(entry => entry.Category == category && entry.Level == level)
                .Select(entry => entry.Message);

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, _entries);

        public void Dispose() { }
    }

    private sealed class RecordingLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception)));
    }
}
