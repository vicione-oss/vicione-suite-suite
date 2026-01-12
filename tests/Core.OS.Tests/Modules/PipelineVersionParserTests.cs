using Core.OS.Modules;
using AwesomeAssertions;
using Xunit;

namespace Core.OS.Tests.Modules;

public class PipelineVersionParserTests
{
    [Theory]
    [InlineData("0.38.1 (40454a3e)", "0.38.1", "40454a3e")]
    [InlineData("v0.38.123 (40454a3e)", "0.38.123", "40454a3e")]
    [InlineData("1.38.1", "1.38.1", null)]
    [InlineData("v0.38.1", "0.38.1", null)]
    public void Should_parse_pipeline_version(string versionString, string expectedVersion, string? expectedCommitId)
    {
        // Act
        var result = PipelineVersionParser.TryParse(versionString, out var parsed, out var commitId);

        // Assert
        result.Should().BeTrue();
        parsed.Should().Be(expectedVersion);
        commitId.Should().Be(expectedCommitId);
    }
}
