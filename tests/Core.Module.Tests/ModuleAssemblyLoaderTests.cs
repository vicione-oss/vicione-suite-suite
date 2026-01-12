using Core.Tests.Tools;
using Sdk.Backend.Modules;
using TestSystem.Backend;
using Xunit;

namespace Core.Module.Tests;

public class ModuleAssemblyLoaderTests
{
    [Trait(Traits.Category, Traits.System)]
    [Fact]
    public void Load_system_module_from_assembly_should_return_module_bundle()
    {
        // Arrange
        var suiteContext = TestFactory.CreateSuiteContext(false, false, false);

        // Act
        var result = ModuleAssemblyLoader.LoadBackendModuleBundles<BackendModule>(suiteContext, typeof(TestSystemModule));

        // Assert
        Assert.Single(result.Bundles);
        Assert.Contains(result.Bundles, k => k.Module is TestSystemModule && Equals(k.Module.ModuleId, "TestSystem.Backend"));
    }

    [Trait(Traits.Category, Traits.System)]
    [Fact]
    public void Load_assembly_from_absolute_directory_should_return_module()
    {
        // Arrange
        var suiteContext = TestFactory.CreateSuiteContext(enableUiHost: false, enableUiModules: false);

        // Act
        var result = ModuleAssemblyLoader.LoadBackendModuleBundles<BackendModule>(suiteContext);

        // Assert
        Assert.Single(result.Bundles);
        Assert.Contains(result.Bundles, k => Equals(k.Module.ModuleId, "TestModule.Backend"));// loaded from directory
    }

    [Trait(Traits.Category, Traits.System)]
    [Fact]
    public void Load_assembly_from_debug_should_return_module()
    {
        // Arrange
        var suiteContext = TestFactory.CreateSuiteContext(enableUiHost: false, enableUiModules: false);

        // Act
        var result = ModuleAssemblyLoader.LoadBackendModuleBundles<BackendModule>(suiteContext);

        // Assert
        Assert.Single(result.Bundles);
        Assert.Contains(result.Bundles, k => Equals(k.Module.ModuleId, "TestModule.Backend"));// loaded from AssemblyPath 
    }

    [Trait(Traits.Category, Traits.System)]
    [Fact]
    public void Disabled_module_should_not_get_loaded()
    {
        // Arrange
        var suiteContext = TestFactory.CreateSuiteContext(false, false, false);

        // Act
        var result = ModuleAssemblyLoader.LoadBackendModuleBundles<BackendModule>(suiteContext);

        // Assert
        Assert.Empty(result.Bundles);
    }
}
