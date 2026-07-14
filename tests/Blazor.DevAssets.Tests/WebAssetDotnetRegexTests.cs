using Xunit;

namespace Blazor.DevAssets.Tests;

public class WebAssetDotnetRegexTests
{
    public sealed class Replace : WebAssetDotnetRegexTests
    {
        [Fact]
        public void Should_strip_version_segment_from_dotnet_js_filename()
        {
            // Arrange
            var input = "dotnet.6.0.0-rc.1.21451.13.js";

            // Act
            var result = WebAssetDotnetRegex.Replace(input);

            // Assert
            Assert.Equal("dotnet.js", result);
        }
    }
}
