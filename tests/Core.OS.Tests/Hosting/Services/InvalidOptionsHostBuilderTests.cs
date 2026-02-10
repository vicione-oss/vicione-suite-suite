using AwesomeAssertions;
using Core.OS.Hosting.Services;
using Xunit;

namespace Core.OS.Tests.Hosting.Services;

public sealed class InvalidOptionsHostBuilderTests
{
    private const string TestUrl = "https://localhost:5500";
    private readonly TimeSpan _testTimeout = TimeSpan.FromMinutes(2);

    [Trait(Traits.Category, Traits.System)]
    [Fact]
    public async Task Should_create_application_returning_html_with_version_information()
    {
        // Arrange        
        using var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(_testTimeout);

        string[] args = [
            $"--urls {TestUrl}"
        ];

        string[] failures = [
            "OptionB is invalid",
            "OptionA is invalid, because of ",
        ];

        // Act                
        var thread = new Thread(async () =>
        {
            var host = InvalidOptionsHostBuilder.Build([], failures);
            host.Urls.Add(TestUrl);

            await host.RunAsync();
        });

        thread.Start();

        await Task.Delay(5000, TestContext.Current.CancellationToken);

        // Assert
        using var client = new HttpClient();
        var htmlResponse = await client.GetStringAsync(new Uri(TestUrl), tokenSource.Token);
        htmlResponse.Should().ContainAll(failures);

        thread.Join();
        await tokenSource.CancelAsync();
    }
}
