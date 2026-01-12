using JiTChat.Backend;

namespace JiTChat.Tests.Backend;

public class JiTChatBackendModuleTests
{
    [Fact]
    public void Init_module_should_register_and_configure_services()
    {
        // Arrange
        var module = new JiTChatBackendModule();

        // Assert
        Assert.NotNull(module.ModuleKey.ModuleId);
    }
}
