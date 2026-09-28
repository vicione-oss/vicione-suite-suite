using System.IO.Abstractions;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Blazor.DevAssets.Tests;

public partial class DevelopmentFileProviderTests
{
    private const string TestModuleName = "ViciOne.Suite.TestModule.Client";

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

    [GeneratedRegex("@import '([^']+)';")]
    private static partial Regex CssImport();

    public sealed class GetFileInfo : DevelopmentFileProviderTests
    {
        private const string FingerprintedBundleRoute = "_content/Library/Library.abcdef1234.bundle.scp.css";

        private readonly MockManifests _manifests = new();

        [Theory]
        [InlineData("js/loadjs.js")]
        [InlineData("js/suite.js")]
        [InlineData("svg/table.svg")]
        [InlineData("_content/ViciOne.Suite.Blazor.Shared/css/suite.css")]
        [InlineData("_content/ViciOne.Suite.Blazor.Shared/js/reconnect.js")]
        [InlineData("_content/ViciOne.Suite.Blazor.Shared/js/loadjs.min.js")]
        [InlineData("_content/ViciOne.Suite.Blazor.Shared/js/suite.js")]
        public void Should_resolve_static_client_asset_requests(string resource)
        {
            // Arrange
            var webassetsJson = GetBlazorSharedStaticWebAssetJson();
            var provider = new DevelopmentFileProvider(new FileSystem());

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
            var provider = new DevelopmentFileProvider(new FileSystem());

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
        [InlineData("_content/ViciOne.Suite.Blazor.Shared/js/suite.js")]
        [InlineData("_content/ViciOne.Suite.Blazor.Shared/css/suite.css")]
        public void Should_resolve_content_asset_when_all_jsons_are_loaded(string resource)
        {
            // Arrange — simulate the real dev scenario where every project's JSON is loaded
            var provider = new DevelopmentFileProvider(new FileSystem());
            provider.AddStaticWebAssetJsons(GetAllStaticWebAssetJsons());

            // Act
            var asset = provider.GetFileInfo(resource);

            // Assert
            Assert.NotNull(asset);
            Assert.True(File.Exists(asset.PhysicalPath), $"Physical file not found: {asset.PhysicalPath}");
        }

        [Fact]
        public void Should_resolve_a_route_whose_file_has_another_name()
        {
            // Arrange
            var provider = new DevelopmentFileProvider(_manifests.FileSystem);
            provider.AddStaticWebAssetJson(_manifests.Write("Host", (FingerprintedBundleRoute, "Library.bundle.scp.css")));

            // Act
            var asset = provider.GetFileInfo($"/{FingerprintedBundleRoute}");

            // Assert
            Assert.True(asset.Exists);
            Assert.Equal("Library.bundle.scp.css", asset.Name);
        }

        [Fact]
        public void Should_not_resolve_a_file_name_that_is_no_route()
        {
            // Arrange
            var provider = new DevelopmentFileProvider(_manifests.FileSystem);
            provider.AddStaticWebAssetJson(_manifests.Write("Host", (FingerprintedBundleRoute, "Library.bundle.scp.css")));

            // Act
            var asset = provider.GetFileInfo("/_content/Library/Library.bundle.scp.css");

            // Assert
            Assert.False(asset.Exists);
        }

        [Theory]
        [InlineData("/_content/Module/Module.styles.css")]
        [InlineData($"/_content/Module/{FingerprintedBundleRoute}")]
        public void Should_resolve_a_request_below_a_project_folder_in_the_project_manifest(string resource)
        {
            // Arrange
            var provider = new DevelopmentFileProvider(_manifests.FileSystem);
            provider.AddStaticWebAssetJson(_manifests.Write("Module",
                ("Module.styles.css", "Module.styles.css"),
                (FingerprintedBundleRoute, "Library.bundle.scp.css")));

            // Act
            var asset = provider.GetFileInfo(resource);

            // Assert
            Assert.True(asset.Exists);
        }

        [Fact]
        public void Should_prefer_the_manifest_of_the_addressed_project()
        {
            // Arrange
            var provider = new DevelopmentFileProvider(_manifests.FileSystem);
            provider.AddStaticWebAssetJson(_manifests.Write("ModuleA", ("js/module.js", "module-a.js")));
            provider.AddStaticWebAssetJson(_manifests.Write("ModuleB", ("js/module.js", "module-b.js")));

            // Act
            var asset = provider.GetFileInfo("/_content/ModuleB/js/module.js");

            // Assert
            Assert.Equal("module-b.js", asset.Name);
        }

        [Fact]
        public void Should_load_every_manifest_of_a_folder()
        {
            // Arrange
            _manifests.Write("ModuleA", ("js/module-a.js", "module-a.js"));
            _manifests.Write("ModuleB", ("js/module-b.js", "module-b.js"));
            var provider = new DevelopmentFileProvider(_manifests.FileSystem);

            // Act
            provider.AddStaticWebAssetJsonsFromPath(_manifests.Folder);

            // Assert
            Assert.True(provider.GetFileInfo("/_content/ModuleA/js/module-a.js").Exists);
            Assert.True(provider.GetFileInfo("/_content/ModuleB/js/module-b.js").Exists);
        }

        [Fact]
        public void Should_read_the_resolved_file_from_the_file_system()
        {
            // Arrange
            var provider = new DevelopmentFileProvider(_manifests.FileSystem);
            provider.AddStaticWebAssetJson(_manifests.Write("Host", (FingerprintedBundleRoute, "Library.bundle.scp.css")));
            var asset = provider.GetFileInfo($"/{FingerprintedBundleRoute}");

            // Act
            using var reader = new StreamReader(asset.CreateReadStream());
            var content = reader.ReadToEnd();

            // Assert
            Assert.Equal(FingerprintedBundleRoute, content);
        }

        /// <summary>
        /// The UI host serves its bundle at the site root and a module below /_content/{Module}/, which is where the
        /// relative imports of each bundle point to. Blazor.Shared is a project reference of the test module, so its
        /// import carries a fingerprint that only exists as a route.
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData($"_content/{TestModuleName}/")]
        public void Should_resolve_every_import_of_a_stylesheet_bundle(string importBase)
        {
            // Arrange
            var provider = new DevelopmentFileProvider(new FileSystem());
            provider.AddStaticWebAssetJsons(GetAllStaticWebAssetJsons());
            var bundle = provider.GetFileInfo($"/_content/{TestModuleName}/{TestModuleName}.styles.css");
            var imports = bundle.Exists
                ? CssImport().Matches(File.ReadAllText(bundle.PhysicalPath!)).Select(match => match.Groups[1].Value).ToList()
                : [];

            // Act
            var unresolved = imports.Where(import => !provider.GetFileInfo($"/{importBase}{import}").Exists).ToList();

            // Assert
            Assert.NotEmpty(imports);
            Assert.Empty(unresolved);
        }
    }
}
