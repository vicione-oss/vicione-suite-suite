using AwesomeAssertions;
using Core.Module.Utils;
using Xunit;

namespace Core.Module.Tests.Utils;

public class VersionUtilsTests
{
    [Fact]
    public void Should_order_versions_correctly()
    {
        // Arrange
        var versions = new List<Version>()
            {
                VersionUtils.ParseVersion("0.1.0.817"),
                VersionUtils.ParseVersion("0.1.0.1183622-ci"),
                VersionUtils.ParseVersion("0.1.0.61017-ci")
            };

        // Act
        var maxVersion = versions.Max();

        // Assert
        Assert.NotNull(maxVersion);
        maxVersion.Should().Be(versions[1]);
    }

    [Theory]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData("1.2.3-ci1733049", 1, 2, 3)]
    [InlineData("1.2.3-rc1", 1, 2, 3)]
    [InlineData("1.2.3+build42", 1, 2, 3)]
    [InlineData("0.1.0.817", 0, 1, 0)]
    public void Should_strip_prerelease_and_build_metadata(string input, int major, int minor, int build)
    {
        // Act
        var success = VersionUtils.TryParseVersion(input, out var version);

        // Assert
        success.Should().BeTrue();
        version!.Major.Should().Be(major);
        version.Minor.Should().Be(minor);
        version.Build.Should().Be(build);
    }

    [Fact]
    public void Should_select_highest_version_with_prerelease_suffixes()
    {
        // Arrange
        var versions = new List<Version>
        {
            VersionUtils.ParseVersion("0.35.0-ci1733049"),
            VersionUtils.ParseVersion("0.35.1-rc1"),
            VersionUtils.ParseVersion("0.34.9")
        };

        // Act
        var maxVersion = versions.Max();

        // Assert
        maxVersion.Should().Be(versions[1]);
    }
}

