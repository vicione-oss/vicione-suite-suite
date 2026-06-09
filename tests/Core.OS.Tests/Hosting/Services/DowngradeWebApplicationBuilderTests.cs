using System.IO.Abstractions.TestingHelpers;
using AwesomeAssertions;
using Core.OS.Hosting.Contracts;
using Core.OS.Hosting.Services;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.Shared.HostManagement;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace Core.OS.Tests.Hosting.Services;

public sealed class DowngradeWebApplicationBuilderTests
{
    private const string TestUrl = "https://localhost:5500";
    private readonly TimeSpan _testTimeout = TimeSpan.FromMinutes(2);
    private readonly MockFileSystem _fileSystem = new();

    private readonly Serilog.ILogger _logger = Substitute.For<Serilog.ILogger>();
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
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        _fileSystem.ResetFileExists(_options).Should().BeTrue();

        thread.Join();
        await tokenSource.CancelAsync();
    }
}
