using System.Reflection;
using Sdk.Client.Modules;
using TestModule.Client;
using Xunit;

namespace Blazor.DevAssets.Tests;

public class ModuleFileProviderTests
{
    [Fact(Skip = "Todo")]
    public void Request_to_module_assets_should_be_resolved()
    {
        // Arrange
        var moduleAssembly = Assembly.GetAssembly(typeof(TestClientModule));
        var moduleDirectory = Path.GetDirectoryName(moduleAssembly!.Location);
        var moduleDllName = moduleAssembly.GetName().Name!;
        var moduleWwwRoot = Path.Combine(moduleDirectory!, "wwwroot");

        // Act
        using var fileProvider = new ModuleFileProvider(moduleDllName, moduleWwwRoot, ModuleAssetHelper.ContentPrefix);

        // Assert
        var file = fileProvider.GetFileInfo($"/{ModuleAssetHelper.ContentPrefix}/{moduleDllName}/svg/test-client-module.svg");
        Assert.NotNull(file);
    }
}
