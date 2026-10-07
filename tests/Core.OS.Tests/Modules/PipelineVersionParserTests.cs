using Core.OS.Modules;

namespace Core.OS.Tests.Modules;

public class PipelineVersionParserTests
{
    [Theory]
    [InlineData("0.38.1 (40454a3e)", "0.38.1", "40454a3e")]
    [InlineData("v0.38.123 (40454a3e)", "0.38.123", "40454a3e")]
    [InlineData("1.38.1", "1.38.1", null)]
    [InlineData("v0.38.1", "0.38.1", null)]
    [InlineData("1.4.0-beta1 (40454a3e)", "1.4.0-beta1", "40454a3e")]
    [InlineData("v1.4.0-rc.2 (40454a3e)", "1.4.0-rc.2", "40454a3e")]
    [InlineData("1.4.0-beta1", "1.4.0-beta1", null)]
    public void Should_parse_pipeline_version(string versionString, string expectedVersion, string? expectedCommitId)
    {
        // Act
        var result = PipelineVersionParser.TryParse(versionString, out var parsed, out var commitId);

        // Assert
        result.Should().BeTrue();
        parsed.Should().Be(expectedVersion);
        commitId.Should().Be(expectedCommitId);
    }

    [Theory]
    [InlineData("4c3b3a3d-1276-directory-build-props")]
    [InlineData("##VERSION_REF##")]
    [InlineData("1.4.0-")]
    [InlineData("1.4.0-beta1 extra")]
    public void Should_reject_non_version_string(string versionString)
    {
        // Act
        var result = PipelineVersionParser.TryParse(versionString, out var parsed, out _);

        // Assert
        result.Should().BeFalse();
        parsed.Should().BeNull();
    }
}
