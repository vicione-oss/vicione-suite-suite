using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reflection;

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
            var provider = new StaticWebAssetContent(new FileSystem(), clientWebAssetsFile);

            // Act
            provider.ParseWebAssetContents();

            // Assert
            Assert.NotEmpty(provider.ContentRoots);
            Assert.True(File.Exists(provider.FindAssetPath(["js", "suite.js"])));
        }

        [Fact]
        public void Should_throw_when_the_manifest_does_not_exist()
        {
            // Arrange
            var provider = new StaticWebAssetContent(new MockFileSystem(), GetBlazorSharedStaticWebAssetJson());

            // Act
            var parse = provider.ParseWebAssetContents;

            // Assert
            Assert.Throws<FileNotFoundException>(parse);
        }

        [Fact]
        public void Should_name_the_content_after_the_manifest_file()
        {
            // Arrange
            var provider = new StaticWebAssetContent(new MockFileSystem(), GetBlazorSharedStaticWebAssetJson());

            // Act
            var name = provider.Name;

            // Assert
            Assert.Equal("ViciOne.Suite.Blazor.Shared", name);
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
