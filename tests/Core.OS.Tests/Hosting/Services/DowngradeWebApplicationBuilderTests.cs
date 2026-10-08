using System.IO.Abstractions.TestingHelpers;
using Core.OS.Hosting.Contracts;
using Core.OS.Hosting.Pages;
using Core.OS.Hosting.Services;
using Core.OS.HostManagement;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.Tests.Security;
using Core.Shared.HostManagement;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CallbackHandlerRegistry = HostManagement.Shared.Communication.NamedPipe.Client.CallbackHandlerRegistry;

namespace Core.OS.Tests.Hosting.Services;

public sealed class DowngradeWebApplicationBuilderTests
{
    private const string TestUrl = "https://localhost:5500";
    private readonly TimeSpan _testTimeout = TimeSpan.FromMinutes(2);
    private readonly MockFileSystem _fileSystem = new();

    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly InstanceOptions _options = new()
    {
        BackupDirectory = "backup",
        CacheDirectory = "cache",
        HomeDirectory = "./home",
        Type = Sdk.Instance.InstanceType.Standalone,
    };

    private readonly HostManagementOptions _hostManagementOptions = new()
    {
        MockClient = new MockPipeClientOptions
        {
            Enabled = true,
        },
    };

    private DowngradeWebApiParameters CreateDowngradeOptions()
    {
        _fileSystem.AddDirectory(_options.HomeDirectory);

        return new DowngradeWebApiParameters
        {
            Instance = _options,
            HostManagement = _hostManagementOptions,
            Logger = _logger,
            DowngradeInformation = new VersionDowngradeInformation("1.23.3", "1.22.19"),
            StopApplicationDelayMs = 5000,
        };
    }

    [Trait(Traits.Category, Traits.System)]
    [Fact]
    public async Task Should_create_application_returning_html_with_version_information()
    {
        // Arrange        
        var builder = WebApplication.CreateBuilder([]);
        builder.Services.AddHttpClient();

        using var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(_testTimeout);

        var downgradeOptions = CreateDowngradeOptions();

        // Act
        await using var host = DowngradeWebApiHostBuilder.Build(builder, _fileSystem, downgradeOptions);
        host.Urls.Add(TestUrl);

        // Assert
        var thread = new Thread(async () =>
        {
            await host.RunAsync(tokenSource.Token);
        });

        thread.Start();

        var factory = host.Services.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient();

        var uri = new Uri($"{host.Urls.First()}");
        var htmlResponse = await client.GetStringAsync(uri, tokenSource.Token);
        htmlResponse.Should().ContainAll([downgradeOptions.DowngradeInformation.CurrentVersion, downgradeOptions.DowngradeInformation.DataVersion]);

        thread.Join();
        await tokenSource.CancelAsync();
    }

    [Trait(Traits.Category, Traits.System)]
    [Fact]
    public async Task Should_create_application_and_stop_on_exit_request()
    {
        // Arrange        
        var builder = WebApplication.CreateBuilder([]);
        builder.Services.AddHttpClient();

        using var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(_testTimeout);

        var downgradeOptions = CreateDowngradeOptions();

        await using var host = DowngradeWebApiHostBuilder.Build(builder, _fileSystem, downgradeOptions);
        host.Urls.Add(TestUrl);

        var thread = new Thread(async () =>
        {
            await host.RunAsync(tokenSource.Token);
        });

        thread.Start();

        var factory = host.Services.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient();

        // Act        
        var response = await client.GetAsync($"{host.Urls.First()}/exit", tokenSource.Token);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        thread.Join();
        await tokenSource.CancelAsync();
    }

    [Trait(Traits.Category, Traits.System)]
    [Fact]
    public async Task Should_create_application_write_reset_flag_and_stop_on_reset_request()
    {
        // Arrange        
        var builder = WebApplication.CreateBuilder([]);
        builder.Services.AddHttpClient();

        using var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(_testTimeout);

        var downgradeOptions = CreateDowngradeOptions();
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        await using var host = DowngradeWebApiHostBuilder.Build(builder, _fileSystem, downgradeOptions);
        host.Urls.Add(TestUrl);

        var thread = new Thread(async () =>
        {
            await host.RunAsync(tokenSource.Token);
        });

        thread.Start();

        await Task.Delay(2000, TestContext.Current.CancellationToken);

        var factory = host.Services.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient();

        // Act                
        var response = await client.GetAsync($"{host.Urls.First()}/reset", tokenSource.Token);

        // Assert
        host.Services.GetService<CallbackHandlerRegistry>().Should().NotBeNull();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        _fileSystem.ResetFileExists(_options).Should().BeTrue();

        thread.Join();
        await tokenSource.CancelAsync();
    }

    [Trait(Traits.Category, Traits.System)]
    [Fact]
    public async Task Should_send_the_startup_failure_policy_allowing_the_stylesheet_it_rendered()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);

        await using var host = DowngradeWebApiHostBuilder.Build(builder, _fileSystem, CreateDowngradeOptions());

        // A port the OS picks, and started here rather than on a thread that is only cancelled, so
        // this case neither waits for nor holds the fixed port the ones above share.
        host.Urls.Add("http://127.0.0.1:0");
        await host.StartAsync(TestContext.Current.CancellationToken);

        using var client = new HttpClient { BaseAddress = new Uri(host.Urls.First()) };

        // Act
        using var response = await client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var policy = response.Headers.GetValues("Content-Security-Policy").Single();

        policy.Should().Contain($"style-src {InlineStyleSheet.HashOf(html)};");
        policy.Should().Contain("script-src 'none';");
        response.Headers.GetValues("X-Frame-Options").Should().Equal("DENY");

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Trait(Traits.Category, Traits.System)]
    [Theory]
    [InlineData("/reset")]
    [InlineData("/exit")]
    public async Task Should_redirect_back_to_the_page_on_action_request(string route)
    {
        // Arrange
        // Development adds the developer exception page, whose inline styles and scripts the startup failure policy blocks.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });

        await using var host = DowngradeWebApiHostBuilder.Build(builder, _fileSystem, CreateDowngradeOptions());
        host.Urls.Add("http://127.0.0.1:0");
        await host.StartAsync(TestContext.Current.CancellationToken);

        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(host.Urls.First()) };

        // Act
        using var response = await client.GetAsync(new Uri(route, UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Redirect);
        response.Headers.Location.Should().Be(new Uri("/", UriKind.Relative));

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Trait(Traits.Category, Traits.System)]
    [Theory]
    [InlineData("/reset")]
    [InlineData("/exit")]
    public async Task Should_explain_instead_of_restarting_when_restart_service_is_disabled(string route)
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);
        var downgradeOptions = CreateDowngradeOptionsWithRestartServiceDisabled();

        await using var host = DowngradeWebApiHostBuilder.Build(builder, _fileSystem, downgradeOptions);
        host.Urls.Add("http://127.0.0.1:0");
        await host.StartAsync(TestContext.Current.CancellationToken);

        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(host.Urls.First()) };

        // Act
        using var response = await client.GetAsync(new Uri(route, UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.Should().Contain(RestartDisabledPage.Text);
        response.Headers.GetValues("Content-Security-Policy").Single().Should().Contain($"style-src {InlineStyleSheet.HashOf(html)};");
        await AssertNoStopRequest(host, downgradeOptions.StopApplicationDelayMs);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Trait(Traits.Category, Traits.System)]
    [Fact]
    public async Task Should_write_the_reset_file_when_restart_service_is_disabled()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);

        await using var host = DowngradeWebApiHostBuilder.Build(builder, _fileSystem, CreateDowngradeOptionsWithRestartServiceDisabled());
        host.Urls.Add("http://127.0.0.1:0");
        await host.StartAsync(TestContext.Current.CancellationToken);

        using var client = new HttpClient { BaseAddress = new Uri(host.Urls.First()) };

        // Act
        using var response = await client.GetAsync(new Uri("/reset", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        _fileSystem.ResetFileExists(_options).Should().BeTrue();

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private DowngradeWebApiParameters CreateDowngradeOptionsWithRestartServiceDisabled()
    {
        const string capabilitiesFile = "SupportedCapabilities.json";
        _fileSystem.AddFile(capabilitiesFile, new MockFileData("""{ "Topics": { "RestartService": "Disabled" } }"""));
        _hostManagementOptions.MockClient!.SupportedCapabilitiesJsonFile = capabilitiesFile;

        return CreateDowngradeOptions() with { StopApplicationDelayMs = 100 };
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
}
