using Core.OS.Persistence;
using Xunit;

namespace Core.OS.Tests.Persistence;

public class EntityTypeCacheTests
{
    [Fact]
    public void GetOrAdd_ReturnsCorrectType_WhenTypeAndAssemblyAreProvided()
    {
        // Arrange
        var expectedType = typeof(string);
        var typeFullName = expectedType.FullName ?? expectedType.Name;
        var assemblyFullName = expectedType.Assembly.FullName;

        // Act
        var result = EntityTypeCache.GetOrAdd(typeFullName, assemblyFullName);

        // Assert
        Assert.Equal(expectedType, result);
    }

    [Fact]
    public void GetOrAdd_ReturnsCorrectType_WhenOnlyTypeIsProvided()
    {
        // Arrange
        var expectedType = typeof(long);
        var typeFullName = expectedType.FullName ?? expectedType.Name;

        // Act
        var result = EntityTypeCache.GetOrAdd(typeFullName, null);

        // Assert
        Assert.Equal(expectedType, result);
    }

    [Fact]
    public void GetOrAdd_ThrowsException_WhenTypeIsNotFound()
    {
        // Arrange
        var typeFullName = "InvalidType";

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => EntityTypeCache.GetOrAdd(typeFullName, null));
    }

    [Fact]
    public void GetOrAdd_ThrowsException_WhenTypeIsNotFoundInSpecifiedAssembly()
    {
        // Arrange
        var typeFullName = typeof(int).FullName!;
        var assemblyFullName = "InvalidAssembly";

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => EntityTypeCache.GetOrAdd(typeFullName, assemblyFullName));
    }
}
