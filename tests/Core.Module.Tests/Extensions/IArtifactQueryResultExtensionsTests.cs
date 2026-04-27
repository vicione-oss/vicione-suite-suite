using AwesomeAssertions;
using Core.Module.Extensions;
using NSubstitute;
using Sdk.Backend.Artifacts;
using Xunit;

namespace Core.Module.Tests.Extensions;

public class IArtifactQueryResultExtensionsTests
{
    private static IArtifact CreateArtifact(string name)
    {
        var artifact = Substitute.For<IArtifact>();
        artifact.Name.Returns(name);
        return artifact;
    }

    private static IArtifactQueryResult CreateQueryResult(params string[] names)
    {
        var artifacts = names.Select(CreateArtifact).ToList();
        var result = Substitute.For<IArtifactQueryResult>();
        result.Artifacts.Returns(artifacts);
        return result;
    }

    public class OrderModuleArtifactsByVersionDesc : IArtifactQueryResultExtensionsTests
    {
        [Fact]
        public void Should_return_empty_when_no_artifacts()
        {
            // Arrange
            var queryResult = CreateQueryResult();

            // Act
            var ordered = queryResult.OrderModuleArtifactsByVersionDesc();

            // Assert
            ordered.Should().BeEmpty();
        }

        [Fact]
        public void Should_accept_filter_condition()
        {
            // Arrange
            var queryResult = CreateQueryResult(
                "0.28.1-ci1523472-linux-arm64_0.25.0.json",
                "0.28.0-linux-arm64_0.25.0.json",
                "0.28.0-ci1522372-linux-arm64_0.25.0.json"
            );

            // Act
            var ordered = queryResult.OrderModuleArtifactsByVersionDesc(k => !k.IsPrerelease).ToList();

            // Assert
            ordered.Should().HaveCount(1);
            ordered[0].Name.Should().Be("0.28.0-linux-arm64_0.25.0.json");
        }

        [Fact]
        public void Should_exclude_artifacts_with_unparseable_names()
        {
            // Arrange
            var queryResult = CreateQueryResult(
                "not-a-valid-name.json",
                "another-invalid.zip"
            );

            // Act
            var ordered = queryResult.OrderModuleArtifactsByVersionDesc();

            // Assert
            ordered.Should().BeEmpty();
        }

        [Fact]
        public void Should_include_all_parseable_artifacts_mixed_with_ci()
        {
            // Arrange
            // All three artifacts have parseable numeric versions, so all are included.
            var queryResult = CreateQueryResult(
                "0.28.0-linux-arm64_0.25.0.json",
                "0.28.1-ci1523472-linux-arm64_0.25.0.json",
                "0.29.0-linux-arm64_0.25.0.json"
            );

            // Act
            var ordered = queryResult.OrderModuleArtifactsByVersionDesc().ToList();

            // Assert
            ordered.Should().HaveCount(3);
            ordered.Select(a => a.Name).Should().Equal(
                "0.29.0-linux-arm64_0.25.0.json",
                "0.28.1-ci1523472-linux-arm64_0.25.0.json",
                "0.28.0-linux-arm64_0.25.0.json"
            );
        }

        [Fact]
        public void Should_order_by_version_descending()
        {
            // Arrange
            var queryResult = CreateQueryResult(
                "0.28.0-linux-arm64_0.25.0.json",
                "0.30.0-linux-arm64_0.25.0.json",
                "0.29.0-linux-arm64_0.25.0.json"
            );

            // Act
            var ordered = queryResult.OrderModuleArtifactsByVersionDesc().ToList();

            // Assert
            ordered.Select(a => a.Name).Should().Equal(
                "0.30.0-linux-arm64_0.25.0.json",
                "0.29.0-linux-arm64_0.25.0.json",
                "0.28.0-linux-arm64_0.25.0.json"
            );
        }

        [Fact]
        public void Should_order_by_version_descending_with_patch_differences()
        {
            // Arrange
            var queryResult = CreateQueryResult(
                "0.28.0-linux-arm64_0.25.0.json",
                "0.28.2-linux-arm64_0.25.0.json",
                "0.28.1-linux-arm64_0.25.0.json"
            );

            // Act
            var ordered = queryResult.OrderModuleArtifactsByVersionDesc().ToList();

            // Assert
            ordered.Select(a => a.Name).Should().Equal(
                "0.28.2-linux-arm64_0.25.0.json",
                "0.28.1-linux-arm64_0.25.0.json",
                "0.28.0-linux-arm64_0.25.0.json"
            );
        }

        [Fact]
        public void Should_order_stable_and_ci_artifacts_together_by_version_desc()
        {
            // Arrange
            // Both have parseable versions; the ci artifact (0.28.1) sorts higher than the stable (0.28.0).
            var queryResult = CreateQueryResult(
                "0.28.0-linux-arm64_0.25.0.json",
                "0.28.1-ci1523472-linux-arm64_0.25.0.json",
                "0.28.1-rc1-linux-arm64_0.25.0.json"
            );

            // Act
            var ordered = queryResult.OrderModuleArtifactsByVersionDesc().ToList();

            // Assert
            ordered.Should().HaveCount(3);
            ordered[0].Name.Should().Be("0.28.1-rc1-linux-arm64_0.25.0.json");
            ordered[1].Name.Should().Be("0.28.1-ci1523472-linux-arm64_0.25.0.json");
            ordered[2].Name.Should().Be("0.28.0-linux-arm64_0.25.0.json");
        }
    }
}
