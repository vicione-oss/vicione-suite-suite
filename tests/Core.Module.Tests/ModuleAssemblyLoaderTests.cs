using Core.Tests.Tools;
using Sdk.Backend.Modules;
using Xunit;

namespace Core.Module.Tests;

public class ModuleAssemblyLoaderTests
{
    [Trait(Traits.Category, Traits.System)]
    [Fact]
    public void Should_return_module_when_loading_assembly_from_absolute_directory()
    {
        // Arrange
        var suiteContext = TestFactory.CreateSuiteContext(enableUiHost: false, enableUiModules: false);

        // Act
        var result = ModuleAssemblyLoader.LoadBackendModuleBundles<BackendModule>(suiteContext);

        // Assert
        Assert.Single(result.Bundles);
        Assert.Contains(result.Bundles, k => Equals(k.Module.ModuleId, "ViciOne.Suite.TestModule"));// loaded from directory
    }

    [Trait(Traits.Category, Traits.System)]
    [Fact]
    public void Should_return_module_when_loading_assembly_from_debug()
    {
        // Arrange
        var suiteContext = TestFactory.CreateSuiteContext(enableUiHost: false, enableUiModules: false);

        // Act
        var result = ModuleAssemblyLoader.LoadBackendModuleBundles<BackendModule>(suiteContext);

        // Assert
        Assert.Single(result.Bundles);
        Assert.Contains(result.Bundles, k => Equals(k.Module.ModuleId, "ViciOne.Suite.TestModule"));// loaded from AssemblyPath 
    }

    [Trait(Traits.Category, Traits.System)]
    [Fact]
    public void Should_not_load_disabled_module()
    {
        // Arrange
        var suiteContext = TestFactory.CreateSuiteContext(false, false, false);

        // Act
        var result = ModuleAssemblyLoader.LoadBackendModuleBundles<BackendModule>(suiteContext);

        // Assert
        Assert.Empty(result.Bundles);
    }
}
