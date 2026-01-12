using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Net;
using System.Net.Http.Json;
using Blazor.Wasm.Backend.Controllers;
using Core.Shared.Logging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;

namespace Blazor.Wasm.Tests.Backend.Controller;

public class LogControllerTests : IClassFixture<TestApplicationFactory<EmptyTestStartup>>
{
    private const string BaseRoute = "/api/log";
    private const string SuiteLogDirectory = "vicione-suite";

    private readonly ILogOptions _logOptionsMock = Substitute.For<ILogOptions>();
    private readonly ILogLevelSwitch _logLevelSwitchMock = Substitute.For<ILogLevelSwitch>();
    private readonly ILogger<LogController> _loggerMock = Substitute.For<ILogger<LogController>>();
    private readonly WebApplicationFactory<EmptyTestStartup> _appFactory;

    public LogControllerTests(TestApplicationFactory<EmptyTestStartup> appFactory)
    {
        var subFolder = Path.Combine(Directory.GetCurrentDirectory(), SuiteLogDirectory);
        if (!Directory.Exists(subFolder))
            Directory.CreateDirectory(subFolder);

        var config = new TestConfig().BuildConfiguration();
        _appFactory = appFactory.WithWebHostBuilder(builder =>
        {
            // base setup done in factory - add/override services needed for the test
            builder.ConfigureTestServices(services =>
            {
                services.ReplaceConfiguration(config)
                    .AddSingleton(_logOptionsMock)
                    .AddSingleton(_logLevelSwitchMock)
                    .AddSingleton(_loggerMock);
                services.Replace(new ServiceDescriptor(typeof(IFileSystem), new MockFileSystem(new Dictionary<string, MockFileData>())));

                services.AddControllers().AddApplicationPart(typeof(LogController).Assembly);
            });
        });
    }

    [Fact]
    public async Task GetLevel()
    {
        // Arrange
        var client = _appFactory.CreateClient();

        // Act
        var level = await client.GetStringAsync($"{BaseRoute}/level");

        // Assert
        Assert.NotNull(level);
    }

    [Theory]
    [InlineData("trace")]
    [InlineData("debug")]
    [InlineData("information")]
    [InlineData("warning")]
    [InlineData("error")]
    [InlineData("critical")]
    [InlineData("none")]
    public async Task SetLevel(string loglevel)
    {
        // Arrange
        var client = _appFactory.CreateClient();

        // Act
        var response = await client.GetAsync($"{BaseRoute}/level/{loglevel}");

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task SetLevelInvalid()
    {
        // Arrange
        var client = _appFactory.CreateClient();

        // Act
        var response = await client.GetAsync($"{BaseRoute}/level/humbug");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

#pragma warning disable xUnit1004 // Test methods should not be skipped
    [Fact(Skip = "MockFileSystem is not used for some reason")]
    public async Task GetSuiteLogs()
    {
        // Arrange
        var client = _appFactory.CreateClient();

        // Act
        var names = await client.GetFromJsonAsync<IEnumerable<string>>($"{BaseRoute}/paths") ?? [];

        // Assert
        Assert.Empty(names);
    }
#pragma warning restore xUnit1004 // Test methods should not be skipped

    [Fact]
    public async Task Get_not_existing_suite_log_should_return_not_found()
    {
        // Arrange
        var client = _appFactory.CreateClient();

        // Act
        var response = await client.GetAsync($"{BaseRoute}/paths/not-existing.log");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
