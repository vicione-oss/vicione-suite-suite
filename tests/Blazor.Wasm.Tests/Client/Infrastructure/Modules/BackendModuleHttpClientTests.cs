using Blazor.Wasm.Client.Infrastructure.Modules;
using Sdk.Modules;
using Sdk.Testing.Client;
using Xunit;

namespace Blazor.Wasm.Tests.Client.Infrastructure.Modules;

public class BackendModuleHttpClientTests
{
    [Fact]
    public async Task GetModuleMetadataList()
    {
        // Arrange
        var response = new List<ModuleMetadata>
        {
            new() { Name = "Test", Version = "12.1.4", MinSuiteSdkVersion = "0.17.0" },
            new() { Name = "Reporting", Version = "3.7.1", MinSuiteSdkVersion = "0.17.0" },
        };

        var httpClient = HttpClientFactory.GetHttpClientWithResponse(response);
        var client = new BackendModuleHttpClient(httpClient);

        // Act
        var moduleInfos = await client.GetModuleMetadata();

        // Assert       
        Assert.Equal(response.Count, moduleInfos.Count);
    }


    [Fact]
    public async Task GetModuleMetadata()
    {
        // Arrange
        var response = new ModuleMetadata { Name = "TestId", Version = "12.1.4", MinSuiteSdkVersion = "0.17.0" };

        var httpClient = HttpClientFactory.GetHttpClientWithResponse(response);
        var client = new BackendModuleHttpClient(httpClient);

        // Act
        var info = await client.GetModuleMetadata("TestId");

        // Assert       
        Assert.NotNull(info);
    }

    [Fact]
    public async Task GetModuleInfoInvalidModule()
    {
        // Arrange
        var response = "";

        var httpClient = HttpClientFactory.GetHttpClientWithResponse(response);
        var client = new BackendModuleHttpClient(httpClient);

        // Act
        var moduleInfo = await client.GetModuleMetadata("TestId");

        // Assert       
        Assert.Null(moduleInfo);
    }
}
