using Core.Module.JFrog;
using Xunit;

namespace Core.Module.Tests.JFrog;

public class JFrogArtifactQueryBuilderTests
{
    public sealed class BuildQueryString
    {
        [Fact]
        public void Should_build_query_with_all_options()
        {
            // Arrange
            var builder = new JFrogArtifactQueryBuilder("my-repo")
                .AndPathMatches("some/path")
                .AndNameMatches("file.txt")
                .IncludeFields(["name", "size"])
                .OrderBy(["name"]);

            // Act
            var result = builder.BuildQueryString();

            // Assert
            Assert.Contains("\"repo\":\"my-repo\"", result);
            Assert.Contains("\"path\":{\"$match\":\"some/path\"}", result);
            Assert.Contains("\"name\":{\"$match\":\"file.txt\"}", result);
            Assert.Contains(".include(\"name\",\"size\")", result);
            Assert.Contains(".sort({\"$asc\":[\"name\"]})", result);
        }

        [Fact]
        public void Should_return_query_without_basic_criteria()
        {
            // Arrange
            var builder = new JFrogArtifactQueryBuilder("my-repo");

            // Act
            var result = builder.BuildQueryString();

            // Assert
            Assert.Contains("\"repo\":\"my-repo\"", result);
            Assert.Contains("items.find", result);
        }
    }


    public sealed class AndPathMatches
    {
        [Fact]
        public void Should_add_path_match_criteria()
        {
            // Arrange
            var builder = new JFrogArtifactQueryBuilder("my-repo");

            // Act
            builder.AndPathMatches("some/path");
            var result = builder.BuildQueryString();

            // Assert
            Assert.Contains("\"path\":{\"$match\":\"some/path\"}", result);
        }
    }

    public sealed class AndNameMatches
    {
        [Fact]
        public void Should_add_name_match_criteria()
        {
            // Arrange
            var builder = new JFrogArtifactQueryBuilder("my-repo");

            // Act
            builder.AndNameMatches("file.txt");
            var result = builder.BuildQueryString();

            // Assert
            Assert.Contains("\"name\":{\"$match\":\"file.txt\"}", result);
        }
    }

    public sealed class AndNameNotMatches
    {
        [Fact]
        public void Should_add_name_not_match_criteria()
        {
            // Arrange
            var builder = new JFrogArtifactQueryBuilder("my-repo");

            // Act
            builder.AndNameNotMatches("file.txt");
            var result = builder.BuildQueryString();

            // Assert
            Assert.Contains("\"name\":{\"$nmatch\":\"file.txt\"}", result);
        }
    }

    public class IncludeFields
    {
        [Fact]
        public void Should_add_includes_to_query()
        {
            // Arrange
            var builder = new JFrogArtifactQueryBuilder("my-repo");

            // Act
            builder.IncludeFields(["name", "size"]);
            var result = builder.BuildQueryString();

            // Assert
            Assert.Contains(".include(\"name\",\"size\")", result);
        }
    }

    public class OrderBy
    {
        [Fact]
        public void Should_add_sort_ascending_for_given_fields()
        {
            // Arrange
            var builder = new JFrogArtifactQueryBuilder("my-repo");

            // Act
            builder.OrderBy(["name", "size"]);
            var result = builder.BuildQueryString();

            // Assert
            Assert.Contains(".sort({\"$asc\":[\"name\",\"size\"]})", result);
        }
    }

    public class OrderByDescending
    {
        [Fact]
        public void Should_add_sort_descending_for_given_fields()
        {
            // Arrange
            var builder = new JFrogArtifactQueryBuilder("my-repo");

            // Act
            builder.OrderByDescending(["created", "modified"]);
            var result = builder.BuildQueryString();

            // Assert
            Assert.Contains(".sort({\"$desc\":[\"created\",\"modified\"]})", result);
        }
    }
}
