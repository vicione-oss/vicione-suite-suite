using Core.Artifacts.JFrog;
using Sdk.Backend.Artifacts;
using Xunit;

namespace Core.Artifacts.Tests;

public class JFrogArtifactQueryBuilderTests
{
    public sealed class BuildQueryString
    {
        [Fact]
        public void Should_build_query_with_all_options()
        {
            // Arrange
            var builder = new JFrogArtifactQueryBuilder()
                .AndPathMatches("some/path")
                .AndNameMatches("file.txt")
                .IncludeFields(["name", "size"])
                .OrderBy(["name"]);

            // Act
            var result = builder.Build();

            // Assert
            Assert.Contains($"\"repo\":\"{JFrogArtifactQueryBuilder.RepositoryPlaceholder}\"", result);
            Assert.Contains("\"path\":{\"$match\":\"some/path\"}", result);
            Assert.Contains("\"name\":{\"$match\":\"file.txt\"}", result);
            Assert.Contains(".include(\"name\",\"size\")", result);
            Assert.Contains(".sort({\"$asc\":[\"name\"]})", result);
        }

        [Fact]
        public void Should_return_query_without_basic_criteria()
        {
            // Arrange
            var builder = new JFrogArtifactQueryBuilder();

            // Act
            var result = builder.Build();

            // Assert
            Assert.Contains($"\"repo\":\"{JFrogArtifactQueryBuilder.RepositoryPlaceholder}\"", result);
            Assert.Contains("items.find", result);
        }
    }

    public sealed class AndPathMatches
    {
        [Fact]
        public void Should_add_path_match_criteria()
        {
            // Arrange
            var builder = new JFrogArtifactQueryBuilder();

            // Act
            builder.AndPathMatches("some/path");
            var result = builder.Build();

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
            var builder = new JFrogArtifactQueryBuilder();

            // Act
            builder.AndNameMatches("file.txt");
            var result = builder.Build();

            // Assert
            Assert.Contains("\"name\":{\"$match\":\"file.txt\"}", result);
        }
    }

    public sealed class AndModifiedAfter
    {
        [Fact]
        public void Should_add_modified_greater_than()
        {
            // Arrange - 16.07.2012 19:20:30
            var builder = new JFrogArtifactQueryBuilder();
            var date = new DateTimeOffset(2012, 7, 16, 19, 20, 30, 45, TimeSpan.FromHours(1));

            // Act
            builder.ModifiedAfter(date);
            var result = builder.Build();

            // Assert
            Assert.Contains("{\"modified\":{\"$gt\":\"2012-07-16T18:20:30.0450000Z\"}}", result);
        }
    }

    public sealed class AndNameNotMatches
    {
        [Fact]
        public void Should_add_name_not_match_criteria()
        {
            // Arrange
            var builder = new JFrogArtifactQueryBuilder();

            // Act
            builder.AndNameNotMatches("file.txt");
            var result = builder.Build();

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
            var builder = new JFrogArtifactQueryBuilder();

            // Act
            builder.IncludeFields(["name", "size"]);
            var result = builder.Build();

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
            var builder = new JFrogArtifactQueryBuilder();

            // Act
            builder.OrderBy(["name", "size"]);
            var result = builder.Build();

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
            var builder = new JFrogArtifactQueryBuilder();

            // Act
            builder.OrderByDescending(["created", "modified"]);
            var result = builder.Build();

            // Assert
            Assert.Contains(".sort({\"$desc\":[\"created\",\"modified\"]})", result);
        }
    }

    public class FilterBy
    {
        [Fact]
        public void Should_add_filter_for_type_folder()
        {
            // Arrange
            var builder = new JFrogArtifactQueryBuilder();

            // Act
            builder.FilterBy(ArtifactKind.Folder);
            var result = builder.Build();

            // Assert
            Assert.Contains("{\"type\":\"folder\"},", result);
        }
    }

    public class Limit
    {
        [Fact]
        public void Should_add_limit_skip_offset_by_default()
        {
            // Arrange
            var builder = new JFrogArtifactQueryBuilder();

            // Act
            builder.Limit(100, 50);
            var result = builder.Build();

            // Assert
            Assert.DoesNotContain(".order(50)", result);
            Assert.Contains(".limit(100)", result);
        }

        [Fact]
        public void Should_add_limit_and_optional_offset()
        {
            // Arrange
            var builder = new JFrogArtifactQueryBuilder();

            // Act
            builder.Limit(100, 50);
            var result = builder.Build();

            // Assert
            Assert.Contains(".offset(50).limit(100)", result);
        }
    }
}
