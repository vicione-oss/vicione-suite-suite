using System.Net;
using AwesomeAssertions;
using Core.OS.Instance.Contracts;
using Core.OS.Instance.HealthCheck;
using Core.OS.Instance.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sdk.Instance;
using Xunit;

namespace Core.OS.Tests.Instance.HealthCheck;

public sealed class SuiteSynchronizationHealthCheckTests
{
    private const string HealthPath = "/health";

    [Fact]
    public async Task Should_return_200_on_master()
    {
        // Arrange
        using var host = new HostBuilder()
            .ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder
                    .UseTestServer()
                    .Configure(app =>
                    {
                        app.UseHealthChecks(HealthPath);
                    })
                    .ConfigureServices(services =>
                    {
                        services.AddSingleton<SynchronizationState>()
                            .AddInstanceServicesMock(InstanceType.Master);

                        services
                            .AddHealthChecks()
                            .AddCheck<SuiteSynchronizationHealthCheck>("Synchronization");
                    });
            })
            .Build();

        // Act
        await host.StartAsync(TestContext.Current.CancellationToken);

        using var server = host.GetTestServer();
        using var client = server.CreateClient();

        // executed in RegisterInstanceConsumer
        host.Services.GetRequiredService<SynchronizationState>().CompleteSynchronization();

        var response = await client.GetAsync(HealthPath, HttpCompletionOption.ResponseContentRead, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.ToString().Should().Be("text/plain");
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("Healthy");
    }

    [Fact]
    public async Task Should_return_200_on_slave_after_synchronization()
    {
        // Arrange
        using var host = new HostBuilder()
            .ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder
                    .UseTestServer()
                    .Configure(app =>
                    {
                        app.UseHealthChecks(HealthPath);
                    })
                    .ConfigureServices(services =>
                    {
                        services
                            .AddSingleton<SynchronizationState>()
                            .AddInstanceServicesMock(InstanceType.Slave);

                        services
                            .AddHealthChecks()
                            .AddCheck<SuiteSynchronizationHealthCheck>("Synchronization");
                    });
            })
            .Build();

        // Act
        await host.StartAsync(TestContext.Current.CancellationToken);

        var server = host.GetTestServer();
        var client = server.CreateClient();

        var firstResponse = await client.GetAsync(HealthPath, TestContext.Current.CancellationToken);
        // executed in RegisterInstanceConsumer
        host.Services.GetRequiredService<SynchronizationState>().CompleteSynchronization();
        var secondResponse = await client.GetAsync(HealthPath, TestContext.Current.CancellationToken);

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.Content.Headers.ContentType?.ToString().Should().Be("text/plain");
        (await secondResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("Healthy");
    }
}
