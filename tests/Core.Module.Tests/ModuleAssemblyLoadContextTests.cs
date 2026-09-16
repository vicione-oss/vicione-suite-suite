using System.Reflection;
using System.Runtime.Loader;
using Sdk.Connections.Contracts;
using Sdk.Modules;
using TestModule.Backend;

namespace Core.Module.Tests;

public class ModuleAssemblyLoadContextTests
{
    [Fact]
    public void Should_load_assembly_into_module_context()
    {
        // Arrange
        var moduleAssembly = Assembly.GetAssembly(typeof(TestBackendModule));
        var contextName = Path.GetFileNameWithoutExtension(moduleAssembly!.Location);
        var suiteContext = new SuiteDependencyContext(new ModuleDependencyContext(ModuleType.Backend, "jsonPath", true),
            null,
            []);

        // Act
        var context = ModuleAssemblyLoadContext.Create(suiteContext, moduleAssembly.Location);

        // Assert
        Assert.Equal(moduleAssembly.GetName().Name, context.Assemblies.First().GetName().Name);
        Assert.NotNull(AssemblyLoadContext.All.FirstOrDefault(k => Equals(k.Name, contextName)));
        Assert.NotNull(context.LoadFromAssemblyName(Assembly.GetAssembly(typeof(Connection))!.GetName()));
    }

    [Fact]
    public void Should_resolve_shared_dependency_to_the_version_shipped_with_each_module()
    {
        // Arrange - two independent modules that each ship a different version of the same
        // transitively-shared assembly (SharedDep). Unique names keep the static load contexts
        // isolated from other tests running in the same process.
        using var fixture = new VersionedAssemblyFixture();
        var suffix = Guid.NewGuid().ToString("N");
        var expectedVersionA = new Version(1, 0, 0, 0);
        var expectedVersionB = new Version(2, 0, 0, 0);

        var moduleAPath = fixture.CreateModule($"SharedDepModuleA_{suffix}", expectedVersionA);
        var moduleBPath = fixture.CreateModule($"SharedDepModuleB_{suffix}", expectedVersionB);

        var core = new ModuleDependencyContext(ModuleType.Backend, "core.deps.json", isDebugSource: false);
        var suiteContext = new SuiteDependencyContext(core, null, []);

        var contextA = ModuleAssemblyLoadContext.Create(suiteContext, moduleAPath);
        var contextB = ModuleAssemblyLoadContext.Create(suiteContext, moduleBPath);

        // Act - request the shared assembly through each module's own load context
        var sharedFromA = contextA.LoadFromAssemblyName(new AssemblyName("SharedDep"));
        var sharedFromB = contextB.LoadFromAssemblyName(new AssemblyName("SharedDep"));

        // Assert - each context resolves the version that was shipped alongside its own module
        Assert.Equal(expectedVersionA, sharedFromA.GetName().Version);
        Assert.Equal(expectedVersionB, sharedFromB.GetName().Version);

        // and the two contexts do not collapse onto a single shared instance/version
        Assert.NotSame(sharedFromA, sharedFromB);
        Assert.NotEqual(sharedFromA.GetName().Version, sharedFromB.GetName().Version);
    }
}
