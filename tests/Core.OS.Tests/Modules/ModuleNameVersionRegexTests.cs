using AwesomeAssertions;
using Core.Module.Utils;
using Semver;
using Xunit;

namespace Core.OS.Tests.Modules;

public class ModuleNameVersionRegexTests
{
    public sealed class GetVersion
    {
        [Fact]
        public void Should_parse_release_version_from_name()
        {
            // Arrange
            var path = "0.24.0-win-x64_0.19.0.json";

            // Act
            var match = ModuleNameVersionRegex.GetModuleVersion(path, out var moduleVersion);

            // Assert
            match.Should().BeTrue();
            moduleVersion.Should().BeEquivalentTo(new SemVersion(0, 24, 0));
        }

        [Fact]
        public void Should_parse_ci_version_from_name()
        {
            // Arrange
            var path = "0.24.0-ci8423423-win-x64_0.19.0.json";

            // Act
            var match = ModuleNameVersionRegex.GetModuleVersion(path, out var moduleVersion);

            // Assert
            match.Should().BeTrue();
            moduleVersion.Should().BeEquivalentTo(new SemVersion(0, 24, 0, ["ci8423423"]));
        }

        [Fact]
        public void Should_parse_rc_version_from_name()
        {
            // Arrange
            var path = "0.24.0-rc5-win-x64_0.19.0.json";

            // Act
            var match = ModuleNameVersionRegex.GetModuleVersion(path, out var moduleVersion);

            // Assert
            match.Should().BeTrue();
            moduleVersion.Should().BeEquivalentTo(new SemVersion(0, 24, 0, ["rc5"]));
        }

        [Fact]
        public void Should_return_false_for_invalid_name()
        {
            // Arrange
            var path = "invalid-name.json";

            // Act
            var match = ModuleNameVersionRegex.GetModuleVersion(path, out var moduleVersion);

            // Assert
            match.Should().BeFalse();
            moduleVersion.Should().BeNull();
        }
    }

    public sealed class TryParse
    {
        [Theory]
        [InlineData("0.24.0-win-x64_0.19.0.json", "0.24.0", "win-x64", "0.19.0")]
        [InlineData("0.24.0-arm64_0.19.0.json", "0.24.0", "arm64", "0.19.0")]
        [InlineData("0.24.0-amd64_1.19.0.json", "0.24.0", "amd64", "1.19.0")]
        [InlineData("0.24.0-ci2546490531-win-x64_2.0.0.json", "0.24.0-ci2546490531", "win-x64", "2.0.0")]
        [InlineData("0.24.0-ci2510650262-win-x64_2.0.1.json", "0.24.0-ci2510650262", "win-x64", "2.0.1")]
        public void Should_parse_release_metadata_file_name(string name, string expectedVersion, string expectedArchitecture, string expectedSdkVersion)
        {
            // Act
            var match = ModuleNameVersionRegex.TryParse(name, out var moduleVersion, out var architecture, out var sdkVersion);

            // Assert
            match.Should().BeTrue();
            moduleVersion.Should().BeEquivalentTo(SemVersion.Parse(expectedVersion));
            architecture.Should().Be(expectedArchitecture);
            sdkVersion.Should().BeEquivalentTo(SemVersion.Parse(expectedSdkVersion));
        }

        [Fact]
        public void Should_parse_ci_metadata_file_name()
        {
            // Arrange
            var name = "0.24.0-ci8423423-arm64_0.19.0.json";

            // Act
            var match = ModuleNameVersionRegex.TryParse(name, out var moduleVersion, out var architecture, out var sdkVersion);

            // Assert
            match.Should().BeTrue();
            moduleVersion.Should().BeEquivalentTo(new SemVersion(0, 24, 0, ["ci8423423"]));
            architecture.Should().Be("arm64");
            sdkVersion.Should().BeEquivalentTo(new SemVersion(0, 19, 0));
        }

        [Fact]
        public void Should_return_false_for_invalid_name()
        {
            // Arrange
            var name = "invalid-name.json";

            // Act
            var match = ModuleNameVersionRegex.TryParse(name, out var moduleVersion, out var architecture, out var sdkVersion);

            // Assert
            match.Should().BeFalse();
            moduleVersion.Should().BeNull();
            architecture.Should().BeNull();
            sdkVersion.Should().BeNull();
        }
    }
}
