using AwesomeAssertions;
using Core.OS.Persistence;
using Xunit;

namespace Core.OS.Tests.Persistence;

public class EntityTypeCacheTests
{
    [Fact]
    public void Should_return_correct_type_when_type_and_assembly_are_provided()
    {
        // Arrange
        var expectedType = typeof(string);
        var typeFullName = expectedType.FullName ?? expectedType.Name;
        var assemblyFullName = expectedType.Assembly.FullName;

        // Act
        var result = EntityTypeCache.GetOrAdd(typeFullName, assemblyFullName);

        // Assert
        result.Should().Be(expectedType);
    }

    [Fact]
    public void Should_return_correct_type_when_only_type_is_provided()
    {
        // Arrange
        var expectedType = typeof(long);
        var typeFullName = expectedType.FullName ?? expectedType.Name;

        // Act
        var result = EntityTypeCache.GetOrAdd(typeFullName, null);

        // Assert
        result.Should().Be(expectedType);
    }

    [Fact]
    public void Should_throw_when_type_is_not_found()
    {
        // Arrange
        var typeFullName = "InvalidType";

        // Act
        var act = () => EntityTypeCache.GetOrAdd(typeFullName, null);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Should_throw_when_type_is_not_found_in_specified_assembly()
    {
        // Arrange
        var typeFullName = typeof(int).FullName!;
        var assemblyFullName = "InvalidAssembly";

        // Act
        var act = () => EntityTypeCache.GetOrAdd(typeFullName, assemblyFullName);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }
}
