using AwesomeAssertions;
using Core.Module.Utils;
using Semver;
using Xunit;

namespace Core.OS.Tests.Modules;

public class ModuleNameVersionRegexTests
{
    [Fact]
    public void Should_parse_versions_from_name()
    {
        // Arrange
        var path = "0.24.0-win-x64_0.19.0.json";

        // Act
        var match = ModuleNameVersionRegex.GetVersion(path, out var moduleVersion);

        // Assert
        Assert.True(match);
        moduleVersion.Should().BeEquivalentTo(new SemVersion(0, 24, 0));
    }
    
    [Fact]
    public void Should_parse_ci_versions_from_name()
    {
        // Arrange
        var path = "0.24.0-ci8423423-win-x64_0.19.0.json";

        // Act
        var match = ModuleNameVersionRegex.GetVersion(path, out var moduleVersion);

        // Assert
        Assert.True(match);
        moduleVersion.Should().BeEquivalentTo(new SemVersion(0, 24, 0, ["ci8423423"]));
    }
}
