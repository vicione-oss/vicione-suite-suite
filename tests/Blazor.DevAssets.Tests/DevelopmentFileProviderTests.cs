using System.Reflection;

namespace Blazor.DevAssets.Tests;

public class DevelopmentFileProviderTests
{
    private static string GetBlazorSharedStaticWebAssetJson()
    {
        const string file = "ViciOne.Suite.Blazor.Shared.staticwebassets.runtime.json";
        return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, file);
    }

    private static string[] GetAllStaticWebAssetJsons()
    {
        var binPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        return Directory.GetFiles(binPath!, "*staticwebassets.runtime.json", SearchOption.TopDirectoryOnly);
    }

    public sealed class GetFileInfo : DevelopmentFileProviderTests
    {
        [Theory]
        [InlineData("bootstrap-external.bs5.min.css")]
        [InlineData("js/loadjs.js")]
        [InlineData("js/static-restart.js")]
        [InlineData("js/suite.js")]
        [InlineData("svg/table.svg")]
        [InlineData("_content/ViciOne.Ui.Shared.Dx/scripts/vicione-ui-shared-dx.min.js")]
        [InlineData("_content/ViciOne.Ui.Shared.Dx/css/bootstrap.min.css")]
        [InlineData("_content/ViciOne.Ui.Shared.Dx/css/dx-bootstrap-custom.css")]
        public void Should_resolve_static_client_asset_requests(string resource)
        {
            // Arrange
            var webassetsJson = GetBlazorSharedStaticWebAssetJson();
            var provider = new DevelopmentFileProvider();

            // Act
            provider.AddStaticWebAssetJson(webassetsJson);

            var asset = provider.GetFileInfo(resource);

            // Assert
            Assert.NotNull(asset);
            Assert.True(File.Exists(asset.PhysicalPath));
        }

        [Theory]
        [InlineData(@"/index.html")]
        [InlineData(@"/js/suite.js")]
        public void Should_resolve_asset_requests_for_external_resources(string resource)
        {
            // Arrange
            var clientWebAssetsFile = GetAllStaticWebAssetJsons();
            var provider = new DevelopmentFileProvider();

            // Act
            provider.AddStaticWebAssetJsons(clientWebAssetsFile);

            // Assert
            Assert.NotNull(provider.GetFileInfo(resource));
        }

        /// <summary>
        /// Regression: _content/ requests must still resolve correctly when ALL project JSONs are loaded.
        /// Previously a .Client string marker was used for disambiguation; now the package name from
        /// _content/{name}/ is matched directly against the JSON filename.
        /// </summary>
        [Theory]
        [InlineData("_content/ViciOne.Ui.Shared.Dx/scripts/vicione-ui-shared-dx.min.js")]
        [InlineData("_content/ViciOne.Ui.Shared.Dx/css/bootstrap.min.css")]
        [InlineData("_content/ViciOne.Ui.Shared.Dx/css/dx-bootstrap-custom.css")]
        public void Should_resolve_content_asset_when_all_jsons_are_loaded(string resource)
        {
            // Arrange — simulate the real dev scenario where every project's JSON is loaded
            var provider = new DevelopmentFileProvider();
            provider.AddStaticWebAssetJsons(GetAllStaticWebAssetJsons());

            // Act
            var asset = provider.GetFileInfo(resource);

            // Assert
            Assert.NotNull(asset);
            Assert.True(File.Exists(asset.PhysicalPath), $"Physical file not found: {asset.PhysicalPath}");
        }
    }

    /// <summary>
    /// Unit tests for the package-name extraction from a _content/ subpath.
    /// This is the core logic that replaces the old broad .Client string marker.
    /// </summary>
    public sealed class ExtractPackageNameFromContentPath : DevelopmentFileProviderTests
    {
        [Theory]
        [InlineData("/_content/ViciOne.Suite.ClusterManagement.Client/icons/bundle.css", "ViciOne.Suite.ClusterManagement.Client")]
        [InlineData("/_content/ViciOne.Ui.Shared.Dx/scripts/foo.js",                    "ViciOne.Ui.Shared.Dx")]
        [InlineData("_content/Some.Package/dir/file.css",                                "Some.Package")]   // no leading slash
        [InlineData("/_content/Single/file.js",                                          "Single")]         // single-segment name
        [InlineData("/js/suite.js",                                                      null)]             // not a _content/ path
        [InlineData("/index.html",                                                       null)]             // not a _content/ path
        public void Should_return_correct_package_name(string subpath, string? expected)
        {
            // Act
            var result = DevelopmentFileProvider.ExtractPackageNameFromContentPath(subpath);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
