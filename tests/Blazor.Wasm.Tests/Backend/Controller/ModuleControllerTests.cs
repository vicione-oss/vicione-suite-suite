using System.Net;
using System.Net.Http.Json;
using Blazor.Wasm.Backend.Controllers;
using Core.Shared.Instance.Contracts;
using Core.UiHosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Sdk.Instance;
using Sdk.Modules;
using Xunit;

namespace Blazor.Wasm.Tests.Backend.Controller;

public class ModuleControllerTests : IClassFixture<TestApplicationFactory<EmptyTestStartup>>
{
    private const string BaseRoute = "/api/module";

    private readonly WebApplicationFactory<EmptyTestStartup> _appFactory;
    private readonly ICookieAccessor _cookieAccessorMock = Substitute.For<ICookieAccessor>();
    private readonly IInstanceInformationProvider _instanceInformationMock = Substitute.For<IInstanceInformationProvider>();
    private readonly IUiHostEnvironment _uiHostEnvironmentMock = Substitute.For<IUiHostEnvironment>();

    public ModuleControllerTests(TestApplicationFactory<EmptyTestStartup> appFactory)
        => _appFactory = appFactory.WithWebHostBuilder(builder =>
        {
            // base setup done in factory - add/override services needed for the test
            builder.ConfigureTestServices(services =>
            {
                services
                    .AddSingleton(_instanceInformationMock)
                    .AddSingleton(_uiHostEnvironmentMock);

                services.AddControllers().AddApplicationPart(typeof(ModuleController).Assembly);

                services.Replace(ServiceDescriptor.Singleton(_cookieAccessorMock));
            });
        });

    [Fact]
    public async Task GetModuleMetadataList()
    {
        // Arrange
        var client = _appFactory.CreateClient();
        _uiHostEnvironmentMock.GetModuleMetadata()
            .Returns(
            [
                new() { Name = "Other", Version = "12.1.4", MinSuiteSdkVersion = "0.17.0" },
                new() { Name = "Another", Version = "0.5.2", MinSuiteSdkVersion = "0.17.0" },
                new() { Name = "Further", Version = "3.7.1", MinSuiteSdkVersion = "0.17.0" },
            ]);

        // Act
        var moduleInfos = await client.GetFromJsonAsync<List<ModuleMetadata>>($"{BaseRoute}/metadata");

        // Assert
        Assert.NotNull(moduleInfos);
        Assert.Equal(3, moduleInfos.Count);
    }

    [Fact]
    public async Task GetModuleMetadata()
    {
        // Arrange
        var client = _appFactory.CreateClient();
        var moduleId = "Test";

        _uiHostEnvironmentMock.GetModuleMetadata()
            .Returns(
            [
                new ModuleMetadata() { Name = "Other", Version = "12.1.4", MinSuiteSdkVersion = "0.17.0" },
                new ModuleMetadata() { Name = moduleId, Version = "0.5.2", MinSuiteSdkVersion = "0.17.0" },
                new ModuleMetadata() { Name = "Further", Version = "3.7.1", MinSuiteSdkVersion = "0.17.0" },
            ]);

        // Act
        var moduleInfo = await client.GetFromJsonAsync<ModuleMetadata>($"{BaseRoute}/metadata/{moduleId}");

        // Assert
        Assert.NotNull(moduleInfo);
        Assert.Equal(moduleId, moduleInfo.Name);
    }

    [Fact]
    public async Task GetLocalInstanceInfo()
    {
        // Arrange
        var client = _appFactory.CreateClient();
        var instanceId = Guid.NewGuid();

        _instanceInformationMock.Local
            .Returns(new InstanceInformation
            {
                Id = instanceId,
                Name = "Test"
            });

        // Act
        var instanceInformation = await client.GetFromJsonAsync<InstanceInformation>($"{BaseRoute}/localInstanceInfo");

        // Assert
        Assert.NotNull(instanceInformation);
        Assert.Equal(instanceId, instanceInformation.Id);
    }

    [Fact]
    public async Task GetModuleInfoInvalidId()
    {
        // Arrange
        var client = _appFactory.CreateClient();

        // Act
        var response = await client.GetAsync($"{BaseRoute}/metadata/XYZ");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Load_modules_archive_should_return_array_if_cookie_is_set()
    {
        // Arrange
        _cookieAccessorMock.HasRequestCookie(Arg.Any<HttpContext>(), Arg.Any<string>())
            .Returns(true);

        _uiHostEnvironmentMock.CreateModulesArchive(Arg.Any<string[]>())
            .Returns(new byte[10]);

        var client = _appFactory.CreateClient();
        var excludedFiles = new List<string>();

        // Act
        var request = await client.PostAsJsonAsync($"{BaseRoute}/load", excludedFiles);

        // Assert
        request.EnsureSuccessStatusCode(); // Status Code 200-299
        Assert.True((await request.Content.ReadAsByteArrayAsync()).Length > 0);
    }

    [Fact]
    public async Task Load_modules_archive_should_return_empty_array_if_no_cookie_is_set()
    {
        // Arrange
        var client = _appFactory.CreateClient();

        // Act
        var request = await client.PostAsJsonAsync($"{BaseRoute}/load", new List<string> { "AssemblyName" });

        // Assert
        request.EnsureSuccessStatusCode(); // Status Code 200-299
        Assert.Empty(await request.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Load_modules_resources_should_return_array_if_cookie_is_set()
    {
        // Arrange
        _cookieAccessorMock.HasRequestCookie(Arg.Any<HttpContext>(), Arg.Any<string>())
            .Returns(true);

        _uiHostEnvironmentMock.CreateModulesResourceArchive("de-DE")
            .Returns(new byte[10]);

        var client = _appFactory.CreateClient();

        // Act
        var response = await client.GetByteArrayAsync($"{BaseRoute}/resource/de-DE");

        // Assert
        Assert.True(response.Length > 0);
    }

    [Fact]
    public async Task Load_modules_resources_should_return_empty_array_if_no_cookie_is_set()
    {
        // Arrange
        var client = _appFactory.CreateClient();

        // Act
        var response = await client.GetByteArrayAsync($"{BaseRoute}/resource/de-DE");

        // Assert
        Assert.Empty(response);
    }

#pragma warning disable xUnit1004 // Test methods should not be skipped
    [Fact(Skip = "Todo")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
    public async Task GetClientModulesShouldReturnZipArchive()
    {
        // Arrange
        var client = _appFactory.CreateClient();

        // Act
        var response = await client.GetAsync($"{BaseRoute}/load");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
