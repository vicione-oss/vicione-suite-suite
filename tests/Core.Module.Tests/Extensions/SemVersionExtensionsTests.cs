using Core.Module.Extensions;
using Semver;

namespace Core.Module.Tests.Extensions;

public class SemVersionExtensionsTests
{
    [Theory]
    [InlineData("0.24.0-ci1733049", true)]
    [InlineData("0.24.0-CI1733049", true)]
    [InlineData("3.0.0-beta2", false)]
    [InlineData("3.0.0-rc1", false)]
    [InlineData("3.0.0", false)]
    public void Should_detect_ci_build(string version, bool expected)
    {
        // Act
        var isCiBuild = SemVersion.Parse(version).IsCiBuild();

        // Assert
        isCiBuild.Should().Be(expected);
    }
}
