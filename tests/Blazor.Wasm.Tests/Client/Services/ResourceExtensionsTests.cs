using AwesomeAssertions;
using Sdk.Client.Contracts;
using Sdk.Client.Extensions;
using Sdk.Client.Modules;
using TestModule.Client;
using Xunit;

namespace Blazor.Wasm.Tests.Client.Services;

public class ResourceExtensionsTests
{
    [Fact]
    public void AddGlobalScript()
    {
        // Act
        var resources = new List<Resource>()
            .AddGlobalScript("Test", "script.js");

        // Assert
        resources
            .Should()
            .ContainSingle(r => r.Bundle == "Test" &&
                r.ResourceType == ResourceType.Script &&
                !string.IsNullOrEmpty(r.Url) && r.Url.StartsWith("/js"));
    }

    [Fact]
    public void AddGlobalStylesheet()
    {
        // Act
        var resources = new List<Resource>()
            .AddGlobalStylesheet("Test", "styles.css");

        // Assert
        resources
            .Should()
            .ContainSingle(r => r.Bundle == "Test" &&
                r.ResourceType == ResourceType.Stylesheet &&
                !string.IsNullOrEmpty(r.Url) && r.Url.StartsWith("/css"));
    }

    [Fact]
    public void AddModuleScript()
    {
        // Act
        var resources = new List<Resource>()
            .AddModuleScript<TestClientModule>("Test", "script.js");

        // Assert
        resources
            .Should()
            .ContainSingle(r => r.Bundle == "Test" &&
                r.ResourceType == ResourceType.Script &&
                !string.IsNullOrEmpty(r.Url) && r.Url.StartsWith($"/{ModuleAssetHelper.ContentPrefix}", StringComparison.Ordinal));
    }

    [Fact]
    public void AddModuleStylesheet()
    {
        // Act
        var resources = new List<Resource>()
            .AddModuleStylesheet<TestClientModule>("Test", "styles.css");

        // Assert
        resources
            .Should()
            .ContainSingle(r => r.Bundle == "Test" &&
                r.ResourceType == ResourceType.Stylesheet &&
                !string.IsNullOrEmpty(r.Url) && r.Url.StartsWith($"/{ModuleAssetHelper.ContentPrefix}", StringComparison.Ordinal));
    }

    [Fact]
    public void AddScript()
    {
        // Arrange
        var scriptUrl = "/path/to/script.js";

        // Act            
        var resources = new List<Resource>()
            .AddScript("Test", scriptUrl);

        // Assert
        resources
            .Should()
            .ContainSingle(r => r.Bundle == "Test" &&
                r.ResourceType == ResourceType.Script &&
                Equals(r.Url, scriptUrl));
    }

    [Fact]
    public void AddStylesheet()
    {
        // Arrange
        var stylesUrl = "/path/to/styles.css";

        // Act            
        var resources = new List<Resource>()
            .AddStylesheet("Test", stylesUrl);

        // Assert
        resources
            .Should()
            .ContainSingle(r => r.Bundle == "Test" &&
                r.ResourceType == ResourceType.Stylesheet &&
                Equals(r.Url, stylesUrl));
    }

    [Fact]
    public void AddComponentScript()
    {
        // Arrange
        var assembly = typeof(TestClientModule).Assembly;
        var location = $"/{ModuleAssetHelper.ContentPrefix}/{assembly.GetName().Name}/";

        // Act
        var resources = new List<Resource>()
            .AddComponentScript("Test", "components.js", assembly);

        // Assert
        resources
            .Should()
            .ContainSingle(r => r.Bundle == "Test" &&
                r.ResourceType == ResourceType.Script &&
                !string.IsNullOrEmpty(r.Url) && r.Url.StartsWith(location));
    }

    [Fact]
    public void AddComponentStylesheet()
    {
        // Arrange
        var assembly = typeof(TestClientModule).Assembly;
        var location = $"/{ModuleAssetHelper.ContentPrefix}/{assembly.GetName().Name}/";

        // Act
        var resources = new List<Resource>()
            .AddComponentStylesheet("Test", "component.css", assembly);

        // Assert
        resources
            .Should()
            .ContainSingle(r => r.Bundle == "Test" &&
                r.ResourceType == ResourceType.Stylesheet &&
                !string.IsNullOrEmpty(r.Url) && r.Url.StartsWith(location));
    }

    [Fact]
    public void AddMultipleResources()
    {
        // Act
        var resources = new List<Resource>()
            .AddModuleStylesheet<TestClientModule>("Test", "styles.css")
            .AddGlobalScript("Test", "global.js");

        // Assert
        resources.Should().HaveCount(2);
    }
}
