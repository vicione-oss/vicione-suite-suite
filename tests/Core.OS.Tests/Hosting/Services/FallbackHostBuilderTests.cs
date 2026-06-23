using System.Net;
using AwesomeAssertions;
using Core.OS.Hosting.Services;
using Xunit;

namespace Core.OS.Tests.Hosting.Services;

public sealed class FallbackHostBuilderTests
{
    private const string TestUrl = "http://localhost:5005";
    private readonly TimeSpan _testTimeout = TimeSpan.FromMinutes(2);

    [Trait(Traits.Category, Traits.System)]
    [Fact]
    public async Task Should_create_application_returning_json_with_version_information()
    {
        // Arrange        
        using var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(_testTimeout);

        string[] args = [
            $"--urls={TestUrl}",
            "--environment=Production"
        ];

        string[] failures = [
            "OptionB is invalid",
            "OptionA is invalid, because of ",
        ];

        // Act                
        var thread = new Thread(async () =>
        {
            var host = FallbackHostBuilder.Build(args, new FallbackHostOptions
            {
                Status = "invalid_options",
                Messages = [.. failures],
                HttpStatusCode = 500,
            });

            await host.RunAsync();
        });

        thread.Start();

        await Task.Delay(5000, TestContext.Current.CancellationToken);

        // Assert
        using var client = new HttpClient();
        using var response = await client.GetAsync(new Uri(TestUrl), tokenSource.Token);
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.InternalServerError);

        var htmlResponse = await response.Content.ReadAsStringAsync(tokenSource.Token);
        htmlResponse.Should().ContainAll(failures);

        thread.Join();
        await tokenSource.CancelAsync();
    }

    [Trait(Traits.Category, Traits.System)]
    [Fact]
    public async Task Should_return_unhealthy_health_check_status()
    {
        // Arrange
        using var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(_testTimeout);

        string[] args = [
            $"--urls={TestUrl}",
            "--environment=Production"
        ];

        string[] failures = [
            "Database unavailable",
            "Config missing",
        ];

        // Act
        var thread = new Thread(async () =>
        {
            var host = FallbackHostBuilder.Build(args, new FallbackHostOptions
            {
                Status = "degraded",
                Messages = [.. failures],
            });

            await host.RunAsync();
        });

        thread.Start();

        await Task.Delay(5000, TestContext.Current.CancellationToken);

        // Assert
        using var client = new HttpClient();
        using var response = await client.GetAsync(new Uri($"{TestUrl}/health"), tokenSource.Token);
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var body = await response.Content.ReadAsStringAsync(tokenSource.Token);
        body.Should().Contain("Unhealthy");

        thread.Join();
        await tokenSource.CancelAsync();
    }
}
