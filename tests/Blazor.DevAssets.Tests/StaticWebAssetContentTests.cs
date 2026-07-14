using System.Reflection;
using Xunit;

namespace Blazor.DevAssets.Tests;

public class StaticWebAssetContentTests
{
    private static string GetBlazorSharedStaticWebAssetJson()
    {
        const string file = "ViciOne.Suite.Blazor.Shared.staticwebassets.runtime.json";
        return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, file);
    }

    public sealed class ParseWebAssetContents : StaticWebAssetContentTests
    {
        [Fact]
        public void Should_parse_static_web_assets_client_json()
        {
            // Arrange
            var clientWebAssetsFile = GetBlazorSharedStaticWebAssetJson();
            var provider = new StaticWebAssetContent(clientWebAssetsFile);

            // Act
            provider.ParseWebAssetContents();

            // Assert
            Assert.NotEmpty(provider.Entries);
            Assert.NotEmpty(provider.ContentRoots);
        }
    }

    public sealed class FixDockerDebugContentRootPaths : StaticWebAssetContentTests
    {
        [Fact]
        public void Should_fix_content_root_paths_for_docker_debug()
        {
            // Arrange
            var inputs = new[]
            {
                "C:\\Users\\xxxx\\.nuget\\packages\\microsoft.aspnetcore.components.webassembly.authentication\\10.0.9\\staticwebassets\\",
                "C:\\vo-suite\\src\\ModuleA.Client\\obj\\Debug\\net10.0\\scopedcss\\projectbundle\\",
                "C:\\vo-suite\\src\\ModuleB.Client\\obj\\Debug\\net10.0\\scopedcss\\projectbundle\\"
            };

            // Act
            var result = StaticWebAssetContent.FixDockerDebugContentRootPaths(inputs);

            // Assert
            Assert.NotEmpty(result);
        }
    }
}
