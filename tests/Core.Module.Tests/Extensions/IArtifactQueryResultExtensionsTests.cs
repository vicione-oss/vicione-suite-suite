using AwesomeAssertions;
using Core.Module.Extensions;
using NSubstitute;
using Sdk.Backend.Artifacts;
using Semver;
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

    public class FilterCompatibleModuleArtifactsBySdkVersion : IArtifactQueryResultExtensionsTests
    {
        [Fact]
        public void Should_return_empty_when_no_artifacts()
        {
            // Arrange
            var queryResult = CreateQueryResult();

            // Act
            var filtered = queryResult.FilterCompatibleModuleArtifactsBySdkVersion(new SemVersion(1, 0, 0));

            // Assert
            filtered.Should().BeEmpty();
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
            var filtered = queryResult.FilterCompatibleModuleArtifactsBySdkVersion(new SemVersion(1, 0, 0));

            // Assert
            filtered.Should().BeEmpty();
        }

        [Fact]
        public void Should_include_artifact_whose_sdk_version_is_compatible()
        {
            // Arrange
            var queryResult = CreateQueryResult(
                "0.29.0-arm64_1.2.0.json",
                "0.28.0-arm64_1.1.0.json"
            );

            // Act
            var filtered = queryResult.FilterCompatibleModuleArtifactsBySdkVersion(new SemVersion(1, 2, 0)).ToList();

            // Assert
            filtered.Should().HaveCount(2);
            filtered[0].Name.Should().Be("0.29.0-arm64_1.2.0.json");
            filtered[1].Name.Should().Be("0.28.0-arm64_1.1.0.json");
        }



        [Fact]
        public void Should_include_artifact_whose_sdk_version_is_good()
        {
            // Arrange
            var queryResult = CreateQueryResult(
                "2.1.2-ci2546490531-win-x64_2.0.0.json",
                "1.0.0-ci2510650262-win-x64_2.0.1.json"
            );

            // Act
            var filtered = queryResult.FilterCompatibleModuleArtifactsBySdkVersion(new SemVersion(2, 1, 0)).ToList();

            // Assert
            filtered.Should().HaveCount(2);
            filtered[0].Name.Should().Be("2.1.2-ci2546490531-win-x64_2.0.0.json");
            filtered[1].Name.Should().Be("1.0.0-ci2510650262-win-x64_2.0.1.json");
        }


        [Fact]
        public void Should_exclude_artifact_whose_sdk_version_is_not_compatible()
        {
            // Arrange
            var queryResult = CreateQueryResult(
                "0.28.0-arm64_2.0.0.json",
                "0.28.0-arm64_1.3.0.json"
            );

            // Act
            var filtered = queryResult.FilterCompatibleModuleArtifactsBySdkVersion(new SemVersion(1, 2, 0));

            // Assert
            filtered.Should().BeEmpty();
        }

        [Fact]
        public void Should_return_only_compatible_artifacts_from_mixed_list()
        {
            // Arrange
            var queryResult = CreateQueryResult(
                "0.28.0-arm64_1.1.0.json",  // compatible: sdk 1.1 <= loaded 1.2
                "0.29.0-arm64_1.2.0.json",  // compatible: sdk 1.2 == loaded 1.2
                "0.30.0-arm64_1.3.0.json"   // incompatible: sdk 1.3 > loaded 1.2
            );

            // Act
            var filtered = queryResult.FilterCompatibleModuleArtifactsBySdkVersion(new SemVersion(1, 2, 0)).ToList();

            // Assert
            filtered.Should().HaveCount(2);
            filtered.Select(a => a.Name).Should().Equal(
                "0.29.0-arm64_1.2.0.json",
                "0.28.0-arm64_1.1.0.json"
            );
        }

        [Fact]
        public void Should_order_compatible_artifacts_by_module_version_descending()
        {
            // Arrange
            var queryResult = CreateQueryResult(
                "0.27.0-arm64_1.0.0.json",
                "0.29.0-arm64_1.2.0.json",
                "0.28.0-arm64_1.1.0.json"
            );

            // Act
            var filtered = queryResult.FilterCompatibleModuleArtifactsBySdkVersion(new SemVersion(1, 2, 0)).ToList();

            // Assert
            filtered.Select(a => a.Name).Should().Equal(
                "0.29.0-arm64_1.2.0.json",
                "0.28.0-arm64_1.1.0.json",
                "0.27.0-arm64_1.0.0.json"
            );
        }
    }
}
