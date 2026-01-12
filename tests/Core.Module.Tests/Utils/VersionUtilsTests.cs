using AwesomeAssertions;
using Core.Module.Utils;
using Xunit;

namespace Core.Module.Tests.Utils;

public class VersionUtilsTests
{
    [Fact]
    public void Evaluate_version_order()
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
}

