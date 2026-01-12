using Core.Module.Utils;
using Xunit;

namespace Core.OS.Tests.Modules;

public class ModuleNameVersionRegexTests
{
    [Fact]
    public void Should_parse_versions_from_name()
    {
        // Arrange
        var path = "0.24.0-ci8423423-win-x64_0.19.0.json";

        // Act
        var match = ModuleNameVersionRegex.GetVersions(path, out var parsedVersion, out var ciVersion);

        // Assert
        Assert.True(match);
        Assert.Equal("0.24.0", parsedVersion);
        Assert.Equal("ci8423423", ciVersion);
    }
}
