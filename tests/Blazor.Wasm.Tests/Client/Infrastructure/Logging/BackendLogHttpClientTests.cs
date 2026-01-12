using Blazor.Wasm.Client.Infrastructure.Logging;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Testing.Client;
using Xunit;

namespace Blazor.Wasm.Tests.Client.Infrastructure.Logging;

public class BackendLogHttpClientTests
{
    private readonly ILogger<BackendLogHttpService> _loggerMock = Substitute.For<ILogger<BackendLogHttpService>>();

    [Fact]
    public async Task GetLogLevel()
    {
        // Arrange
        var response = nameof(LogLevel.Error);

        var httpClient = HttpClientFactory.GetHttpClientWithResponse(response);
        var client = new BackendLogHttpService(httpClient, _loggerMock);

        // Act
        var level = await client.GetLogLevel();

        // Assert       
        Assert.Equal(response, level.ToString());
    }


    [Fact]
    public async Task GetLogNames()
    {
        // Arrange
        var response = new[] { "Logfile1", "Logfile2" };

        var httpClient = HttpClientFactory.GetHttpClientWithResponse(response);
        var client = new BackendLogHttpService(httpClient, _loggerMock);

        // Act
        var names = await client.GetLogPaths();

        // Assert       
        Assert.Equal(response, names);
    }
}
