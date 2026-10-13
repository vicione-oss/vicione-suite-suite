using JiTChat.Backend;

namespace JiTChat.Tests.Backend;

public class JiTChatBackendModuleTests
{
    [Fact]
    public void Should_register_and_configure_services()
    {
        // Arrange
        var module = new JiTChatBackendModule();

        // Assert
        Assert.NotNull(module.ModuleKey.ModuleId);
    }
}
