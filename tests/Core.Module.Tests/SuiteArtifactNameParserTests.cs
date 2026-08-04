using Semver;

namespace Core.Module.Tests;

public sealed class SuiteArtifactNameParserTests
{
    public sealed class TryParse
    {
        [Theory]
        [InlineData("vicione-suite_1.0.4_arm64.deb", "1.0.4", "arm64")]
        [InlineData("vicione-suite_1.1.0_amd64.deb", "1.1.0", "amd64")]
        [InlineData("vicione-suite_1.1.0~3423423_amd64.deb", "1.1.0-3423423", "amd64")]
        [InlineData("vicione-suite_1.0.4_arm64.deb.minisig", "1.0.4", "arm64")]
        [InlineData("vicione-suite_1.50.5_amd64.deb.minisig", "1.50.5", "amd64")]
        [InlineData("vicione-suite_1.1.0~3423423_amd64.deb.minisig", "1.1.0-3423423", "amd64")]
        public void Should_return_true_on_parse_valid_package_name(string input, string expectedSuiteVersion, string expectedArchitecture)
        {
            // Act
            var result = SuiteArtifactNameParser.TryParse(input, out var suiteVersion, out var architecture);

            // Assert
            result.Should().BeTrue();

            suiteVersion.Should().Be(SemVersion.Parse(expectedSuiteVersion));
            architecture.Should().Be(expectedArchitecture);
        }

        [Theory]
        [InlineData("vicie-suite_1.0.4_arm64.deb")]
        [InlineData("vicione_suite_1.0_amd64.deb")]
        [InlineData("vicione-suite_1.0.4_amd32_1.2.1.deb.minisig")]
        [InlineData("vicione-suite_1.1.0_amd64_1.2.1.exe.minisig")]
        [InlineData("vicione-suite_1.1.0_amd64_1.2.1.exe")]
        [InlineData("vicione-suite_1.1.0_x64.exe")]
        [InlineData("vicione-suite_2.34.45-ci234234_x64.exe")]
        [InlineData("vicione-suite_1.xx.3_x64.deb")]
        public void Should_return_false_on_parse_invalid_package_name(string input)
        {
            // Act
            var result = SuiteArtifactNameParser.TryParse(input, out var suiteVersion, out var architecture);

            // Assert
            result.Should().BeFalse();
            suiteVersion.Should().BeNull();
            architecture.Should().BeNull();
        }
    }

    public sealed class TryParseMetadata
    {
        [Theory]
        [InlineData("vicione-suite_1.0.4_arm64_1.1.1.json", "1.0.4", "arm64", "1.1.1")]
        [InlineData("vicione-suite_1.1.0_amd64_1.2.1.json", "1.1.0", "amd64", "1.2.1")]
        [InlineData("vicione-suite_1.1.0~3423423_amd64_1.2.1.json", "1.1.0-3423423", "amd64", "1.2.1")]
        [InlineData("vicione-suite_1.1.0~rc1_amd64_1.2.1.json", "1.1.0-rc1", "amd64", "1.2.1")]
        public void Should_return_true_on_parse_valid_package_name(string input, string expectedSuiteVersion, string expectedArchitecture, string expectedHmVersion)
        {
            // Act
            var result = SuiteArtifactNameParser.TryParseMetadata(input, out var suiteVersion, out var architecture, out var hmVersion);

            // Assert
            result.Should().BeTrue();

            suiteVersion.Should().Be(SemVersion.Parse(expectedSuiteVersion));
            hmVersion.Should().Be(SemVersion.Parse(expectedHmVersion));
            architecture.Should().Be(expectedArchitecture);
        }

        [Theory]
        [InlineData("vicie-suite_1.0.4_arm64_1.1.1.json")]
        [InlineData("vicione-suite_1.0_amd64_1.2.1.json")]
        [InlineData("vicione-suite_1.0.4_amd32_1.1.1.deb")]
        [InlineData("vicione-suite_1.1.0_amd64_1.2.deb.minisig")]
        [InlineData("vicione-suite_1.1.0_amd64_1.2.1.exe.minisig")]
        [InlineData("vicione-suite_1.1.0_amd64_1.2.1.exe")]
        [InlineData("vicione-suite_1.1.0_x64_1.2.1.json")]
        [InlineData("vicione-suite_2.34.45-ci234234_x64_1.2.1.exe")]
        [InlineData("vicione-suite_1.xx.3_x64_1.2.1.exe")]
        public void Should_return_false_on_parse_invalid_package_name(string input)
        {
            // Act
            var result = SuiteArtifactNameParser.TryParseMetadata(input, out var suiteVersion, out var architecture, out var hmVersion);

            // Assert
            result.Should().BeFalse();
            suiteVersion.Should().BeNull();
            architecture.Should().BeNull();
            hmVersion.Should().BeNull();
        }
    }
}
