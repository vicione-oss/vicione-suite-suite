using Sdk.Client.Modules;
using System.Reflection;
using TestModule.Client;

namespace Blazor.DevAssets.Tests;

public class ModuleFileProviderTests
{
    [Fact(Skip = "Todo")]
    public void Should_resolve_request_to_module_assets()
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
