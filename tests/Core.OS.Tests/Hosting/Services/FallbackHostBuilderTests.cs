using System.Net;
using System.IO.Abstractions.TestingHelpers;
using Core.OS.EnvironmentOverrides;
using Core.OS.Hosting.Extensions;
using Core.OS.Hosting.Pages;
using Core.OS.Hosting.Services;
using Core.OS.Instance;
using Core.OS.HostManagement;
using Core.OS.Instance.Extensions;
using Core.OS.Security;
using Core.OS.Tests.EnvironmentOverrides;
using Core.OS.Tests.Security;
using Core.Shared.EnvironmentOverrides;
using Core.Shared.HostManagement;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.OS.Tests.Hosting.Services;

// The debug page reads the override switch from the process environment, which the whole assembly
// shares.
[Collection(EnvironmentOverridesCollectionDefinition.Name)]
public sealed class FallbackHostBuilderTests : IDisposable
{
    private const string ContentSecurityPolicyHeader = "Content-Security-Policy";

    private readonly string? _previousEnv =
        Environment.GetEnvironmentVariable(EnvironmentOverridesSwitch.EnabledEnvironmentVariable);

    public void Dispose()
        => Environment.SetEnvironmentVariable(EnvironmentOverridesSwitch.EnabledEnvironmentVariable, _previousEnv);

    [Trait(Traits.Category, Traits.System)]
    [Fact]
    public async Task Should_render_the_debug_page_as_html_with_the_diagnostic_groups()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var instanceOptions = CreateInstanceOptions();
        fileSystem.AddDirectory(instanceOptions.HomeDirectory);
        fileSystem.AddFile(fileSystem.GetLocalDataVersionFilePath(instanceOptions), new MockFileData("1.2.9"));
        fileSystem.AddFile(
            fileSystem.GetLocalRecoveryFilePath(instanceOptions),
            new MockFileData("{\"lastStartup\":\"2026-08-31T10:15:00+00:00\",\"startups\":4,\"recoveryApplied\":true}"));

        var options = new FallbackHostOptions
        {
            Status = FallbackHostStatus.InvalidOptions,
            Messages = ["OptionA is invalid", "OptionB is invalid"],
            Logger = NullLogger.Instance,
            HttpStatusCode = 500,
            FileSystem = fileSystem,
            Instance = instanceOptions,
        };

        // Act
        using var response = await Request(options, client => client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        response.Content.Headers.ContentType?.CharSet.Should().Be("utf-8");

        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.Should().StartWith("<!DOCTYPE html>");
        html.Should().ContainAll(
            FallbackHostStatus.InvalidOptions,
            "OptionA is invalid",
            "OptionB is invalid",
            "1.2.9",
            "Recovery state");
    }

    [Trait(Traits.Category, Traits.System)]
    [Fact]
    public async Task Should_render_the_debug_page_for_the_recovery_exhausted_status()
    {
        // Arrange
        var options = CreateRecoveryExhaustedOptions();

        // Act
        using var response = await Request(options, client => client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");

        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.Should().ContainAll(
            FallbackHostStatus.RecoveryExhausted,
            "Recovery was already applied and the suite keeps crashing");
    }

    [Trait(Traits.Category, Traits.System)]
    [Fact]
    public async Task Should_render_override_keys_without_their_values()
    {
        // Arrange
        Environment.SetEnvironmentVariable(EnvironmentOverridesSwitch.EnabledEnvironmentVariable, "true");

        var fileSystem = new MockFileSystem();
        var instanceOptions = CreateInstanceOptions();
        fileSystem.AddDirectory(instanceOptions.HomeDirectory);
        fileSystem.AddFile(
            EnvironmentOverridesFile.RequirePath(fileSystem, instanceOptions.HomeDirectory),
            new MockFileData(EnvironmentOverridesFormat.Serialize(new Dictionary<string, string>
            {
                ["Authentication__ClientSecret"] = "s3cret-that-must-not-be-rendered",
            })));

        var options = new FallbackHostOptions
        {
            Status = FallbackHostStatus.InvalidOptions,
            Messages = ["Authentication is incompletely configured"],
            Logger = NullLogger.Instance,
            HttpStatusCode = 500,
            FileSystem = fileSystem,
            Instance = instanceOptions,
        };

        // Act
        using var response = await Request(options, client => client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken));

        // Assert
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.Should().Contain("Authentication__ClientSecret");
        html.Should().NotContain("s3cret-that-must-not-be-rendered");
    }

    [Trait(Traits.Category, Traits.System)]
    [Fact]
    public async Task Should_return_unhealthy_health_check_status()
    {
        // Arrange
        var options = new FallbackHostOptions
        {
            Status = FallbackHostStatus.RecoveryExhausted,
            Messages = ["Database unavailable", "Config missing"],
            Logger = NullLogger.Instance,
        };

        // Act
        using var response = await Request(options, client => client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("Unhealthy");
    }

    [Trait(Traits.Category, Traits.System)]
    [Fact]
    public async Task Should_move_the_override_file_aside_and_request_a_restart_on_the_disable_action()
    {
        // Arrange
        Environment.SetEnvironmentVariable(EnvironmentOverridesSwitch.EnabledEnvironmentVariable, "true");

        var fileSystem = new MockFileSystem();
        var instanceOptions = CreateInstanceOptions();
        fileSystem.AddDirectory(instanceOptions.HomeDirectory);

        var path = EnvironmentOverridesFile.RequirePath(fileSystem, instanceOptions.HomeDirectory);
        var disabledPath = EnvironmentOverridesFile.ResolveDisabledPath(fileSystem, instanceOptions.HomeDirectory)!;
        var contents = EnvironmentOverridesFormat.Serialize(new Dictionary<string, string>
        {
            ["Authentication__ClientSecret"] = "s3cret",
        });
        fileSystem.AddFile(path, new MockFileData(contents));

        var options = new FallbackHostOptions
        {
            Status = FallbackHostStatus.InvalidOptions,
            Messages = ["Authentication is incompletely configured"],
            Logger = NullLogger.Instance,
            HttpStatusCode = 500,
            FileSystem = fileSystem,
            Instance = instanceOptions,
            HostManagement = new HostManagementOptions { MockClient = new MockPipeClientOptions { Enabled = true } },
            StopApplicationDelayMs = 100,
        };

        // Act
        await WithHost(options, async (host, client) =>
        {
            using var response = await client.PostAsync(
                new Uri(FallbackHostBuilder.DisableEnvironmentOverridesRoute, UriKind.Relative),
                content: null,
                TestContext.Current.CancellationToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Found);
            response.Headers.Location?.OriginalString.Should().Be("/");

            fileSystem.File.Exists(path).Should().BeFalse();
            fileSystem.File.Exists(disabledPath).Should().BeTrue();
            (await fileSystem.File.ReadAllTextAsync(disabledPath, TestContext.Current.CancellationToken)).Should().Be(contents);

            // MockPipeClient answers a restart request by stopping the application, the way host
            // management would have the service manager do it.
            await WaitForStopRequest(host);
        });
    }

    [Trait(Traits.Category, Traits.System)]
    [Fact]
    public async Task Should_move_the_override_file_aside_and_explain_instead_of_restarting_when_restart_service_is_disabled()
    {
        // Arrange
        Environment.SetEnvironmentVariable(EnvironmentOverridesSwitch.EnabledEnvironmentVariable, "true");

        var fileSystem = new MockFileSystem();
        var instanceOptions = CreateInstanceOptions();
        fileSystem.AddDirectory(instanceOptions.HomeDirectory);

        var path = EnvironmentOverridesFile.RequirePath(fileSystem, instanceOptions.HomeDirectory);
        var disabledPath = EnvironmentOverridesFile.ResolveDisabledPath(fileSystem, instanceOptions.HomeDirectory)!;
        fileSystem.AddFile(path, new MockFileData(EnvironmentOverridesFormat.Serialize(new Dictionary<string, string> { ["A"] = "b" })));

        const string capabilitiesFile = "SupportedCapabilities.json";
        fileSystem.AddFile(capabilitiesFile, new MockFileData("""{ "Topics": { "RestartService": "Disabled" } }"""));

        var options = new FallbackHostOptions
        {
            Status = FallbackHostStatus.InvalidOptions,
            Messages = ["Authentication is incompletely configured"],
            Logger = NullLogger.Instance,
            HttpStatusCode = 500,
            FileSystem = fileSystem,
            Instance = instanceOptions,
            HostManagement = new HostManagementOptions
            {
                MockClient = new MockPipeClientOptions { Enabled = true, SupportedCapabilitiesJsonFile = capabilitiesFile }
            },
            StopApplicationDelayMs = 100,
        };

        // Act
        await WithHost(options, async (host, client) =>
        {
            using var response = await client.PostAsync(
                new Uri(FallbackHostBuilder.DisableEnvironmentOverridesRoute, UriKind.Relative),
                content: null,
                TestContext.Current.CancellationToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            html.Should().Contain(RestartDisabledPage.Text);
            response.Headers.GetValues(ContentSecurityPolicyHeader).Single().Should().Contain($"style-src {InlineStyleSheet.HashOf(html)};");

            fileSystem.File.Exists(path).Should().BeFalse();
            fileSystem.File.Exists(disabledPath).Should().BeTrue();

            await AssertNoStopRequest(host, options.StopApplicationDelayMs);
        });
    }

    [Trait(Traits.Category, Traits.System)]
    [Fact]
    public async Task Should_not_offer_the_disable_action_without_host_management_options()
    {
        // Arrange
        Environment.SetEnvironmentVariable(EnvironmentOverridesSwitch.EnabledEnvironmentVariable, "true");

        var fileSystem = new MockFileSystem();
        var instanceOptions = CreateInstanceOptions();
        fileSystem.AddDirectory(instanceOptions.HomeDirectory);
        fileSystem.AddFile(
            EnvironmentOverridesFile.RequirePath(fileSystem, instanceOptions.HomeDirectory),
            new MockFileData(EnvironmentOverridesFormat.Serialize(new Dictionary<string, string> { ["A"] = "b" })));

        var options = new FallbackHostOptions
        {
            Status = FallbackHostStatus.InvalidOptions,
            Messages = ["Authentication is incompletely configured"],
            Logger = NullLogger.Instance,
            HttpStatusCode = 500,
            FileSystem = fileSystem,
            Instance = instanceOptions,
        };

        // Act
        using var response = await Request(options, client => client.PostAsync(
            new Uri(FallbackHostBuilder.DisableEnvironmentOverridesRoute, UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Trait(Traits.Category, Traits.System)]
    [Fact]
    public async Task Should_send_the_startup_failure_policy()
    {
        // Arrange
        var options = CreateRecoveryExhaustedOptions();

        // Act
        using var response = await Request(options, client => client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken));

        // Assert
        response.Headers.GetValues(ContentSecurityPolicyHeader).Should().Equal(
            ContentSecurityPolicy.ForStartupFailurePage(StartupFailurePageStyles.FailsafePage));
        response.Headers.GetValues("X-Frame-Options").Should().Equal("DENY");
    }

    [Trait(Traits.Category, Traits.System)]
    [Fact]
    public async Task Should_allow_the_stylesheet_it_rendered()
    {
        // Arrange
        var options = CreateRecoveryExhaustedOptions();

        // Act
        using var response = await Request(options, client => client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken));

        // Assert
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var policy = response.Headers.GetValues(ContentSecurityPolicyHeader).Single();

        policy.Should().Contain($"style-src {InlineStyleSheet.HashOf(html)};");
    }

    private static async Task WaitForStopRequest(WebApplication host)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!host.Lifetime.ApplicationStopping.IsCancellationRequested && !timeout.IsCancellationRequested)
        {
            await Task.Delay(50, timeout.Token);
        }

        host.Lifetime.ApplicationStopping.IsCancellationRequested.Should().BeTrue();
    }

    /// <summary>
    /// MockPipeClient answers a restart request by stopping the application, so the host still runs
    /// well after the restart delay only if no restart was requested.
    /// </summary>
    private static async Task AssertNoStopRequest(WebApplication host, int stopApplicationDelayMs)
    {
        await Task.Delay(stopApplicationDelayMs * 10, TestContext.Current.CancellationToken);

        host.Lifetime.ApplicationStopping.IsCancellationRequested.Should().BeFalse();
    }

    private static FallbackHostOptions CreateRecoveryExhaustedOptions() => new()
    {
        Status = FallbackHostStatus.RecoveryExhausted,
        Messages = ["Recovery was already applied and the suite keeps crashing"],
        Logger = NullLogger.Instance,
        HttpStatusCode = 503,
    };

    private static InstanceOptions CreateInstanceOptions() => new()
    {
        BackupDirectory = "backup",
        CacheDirectory = "cache",
        HomeDirectory = "./home",
        Type = Sdk.Instance.InstanceType.Standalone,
    };

    /// <summary>
    /// Runs the failsafe host on a port the OS picks and stops it again, so the cases here can run
    /// next to each other.
    /// </summary>
    private static async Task<HttpResponseMessage> Request(FallbackHostOptions options, Func<HttpClient, Task<HttpResponseMessage>> request)
    {
        HttpResponseMessage? response = null;
        await WithHost(options, async (_, client) => response = await request(client));

        return response!;
    }

    private static async Task WithHost(FallbackHostOptions options, Func<WebApplication, HttpClient, Task> act)
    {
        string[] args = [
            "--urls=http://127.0.0.1:0",
            "--environment=Production"
        ];

        var host = FallbackHostBuilder.Build(args, options);
        await host.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var client = new HttpClient(handler) { BaseAddress = new Uri(host.Urls.First()) };
            await act(host, client);
        }
        finally
        {
            await host.StopAsync(TestContext.Current.CancellationToken);
            await host.DisposeAsync();
        }
    }
}
