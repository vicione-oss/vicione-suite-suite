using Blazor.Wasm.Client.Infrastructure.SignalR;
using Xunit;

namespace Blazor.Wasm.Tests.Client.Infrastructure.SignalR;

public class MessageTypeCacheTests
{
    [Fact]
    public void GetOrAdd_ReturnsCorrectType()
    {
        // Arrange
        var expectedType = typeof(string);
        var typeFullName = expectedType.FullName ?? expectedType.Name;

        // Act
        var result = MessageTypeCache.GetOrAdd(typeFullName);

        // Assert
        Assert.Equal(expectedType, result);
    }

    [Fact]
    public void GetOrAdd_ThrowsException_WhenTypeIsNotFound()
    {
        // Arrange
        const string typeFullName = "InvalidType";

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => MessageTypeCache.GetOrAdd(typeFullName));
    }
}
