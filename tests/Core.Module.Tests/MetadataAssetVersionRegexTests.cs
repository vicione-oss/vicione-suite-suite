using Xunit;

namespace Core.Module.Tests;

public class MetadataAssetVersionRegexTests
{
    [Fact]
    public void Should_parse_ci_module_version()
    {
        // Arrange
        var version = "12.1.3-ci1399909";


        // Act
        var matched = MetadataAssetVersionRegex.GetVersions(version, out var parsedVersion, out var ciVersion);

        // Assert
        Assert.True(matched);
        Assert.Equal("12.1.3", parsedVersion);
        Assert.Equal("ci1399909", ciVersion);
    }

    [Theory]
    [InlineData("12.1.3.3-ci1399909")]
    [InlineData(".1.3.3-ci1399909")]
    [InlineData("1.3.3-1399909")]
    [InlineData(".1.3.3-1399909ci")]
    public void Should_not_match_wrong_formats(string version)
    {
        // Act
        var matched = MetadataAssetVersionRegex.GetVersions(version, out _, out _);

        // Assert
        Assert.False(matched);
    }
}
