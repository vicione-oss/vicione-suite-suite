using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Blazor.DevAssets.Tests;

public partial class DevelopmentFileProviderTests
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
    public void Provider_should_resolve_static_client_asset_requests(string resource)
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
    public void Provider_should_resolve_asset_requests_for_external_resources(string resource)
    {
        // Arrange
        var clientWebAssetsFile = GetAllStaticWebAssetJsons();
        var provider = new DevelopmentFileProvider();

        // Act
        provider.AddStaticWebAssetJsons(clientWebAssetsFile);

        // Assert
        Assert.NotNull(provider.GetFileInfo(resource));
    }

    [Fact]
    public void ParseStaticWebAssetsClientJson()
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


    [Fact]
    public void GetRelativeParentPath()
    {
        var path = "/_content/ViciOne.Suite.Module.Client/svg/toolbox.svg";
        var normalized = PathHelper.NormalizePath(path);

        var parent = Directory.GetParent(normalized);
        var relative = Path.Combine(parent!.Name, Path.GetFileName(normalized));
        var expected = Path.Combine("svg", "toolbox.svg");

        Assert.Equal(expected, relative);
    }


    [Fact]
    public void RegexDotnetTest()
    {
        var input = "dotnet.6.0.0-rc.1.21451.13.js";
        var regex = MyRegex();

        var result = regex.Replace(input, delegate (Match match)
        {
            return input.Replace(match.Groups[1].Value, "", StringComparison.Ordinal);
        });

        Assert.Equal("dotnet.js", result);
    }


    [Fact]
    public void RegexContentRootTest()
    {
        var inputs = new[]
        {
            "C:\\Users\\xxxx\\.nuget\\packages\\microsoft.aspnetcore.components.webassembly.authentication\\9.0.5\\staticwebassets\\",
            "D:\\repos\\vo-suite\\src\\ModuleA.Client\\obj\\Debug\\net10.0\\scopedcss\\projectbundle\\",
            "D:\\repos\\vo-suite\\src\\ModuleB.Client\\obj\\Debug\\net10.0\\scopedcss\\projectbundle\\"
        };

        var result = StaticWebAssetContent.FixDockerDebugContentRootPaths(inputs);

        Assert.NotEmpty(result);
    }


#if WIN64
        [Fact]
        public void NormalizePath()
        {
            var input1 = @"js/location.js";
            var input2 = @"js\\location.js";

            var normalized1 = PathHelper.NormalizePath(input1);
            var normalized2 = PathHelper.NormalizePath(input2);

            Assert.Equal(normalized1, normalized2);
        }
#endif

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

    [GeneratedRegex(@"dotnet([0-9\.\-rc]+)\.js", RegexOptions.Compiled)]
    private static partial Regex MyRegex();
}
