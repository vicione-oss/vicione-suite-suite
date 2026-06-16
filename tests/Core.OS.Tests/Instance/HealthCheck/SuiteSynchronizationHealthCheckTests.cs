using System.Net;
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

public class SuiteSynchronizationHealthCheckTests
{
    private const string HealthPath = "/health";

    [Fact]
    public async Task Status_code_should_be_200_on_master()
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
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.ToString());
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Status_code_should_be_200_on_slave_after_synchronization()
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
        Assert.Equal(HttpStatusCode.ServiceUnavailable, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal("text/plain", secondResponse.Content.Headers.ContentType?.ToString());
        Assert.Equal("Healthy", await secondResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
