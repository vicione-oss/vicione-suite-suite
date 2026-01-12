using System.Reflection;
using System.Runtime.Loader;
using Sdk.Connections.Contracts;
using Sdk.Modules;
using TestModule.Backend;
using Xunit;

namespace Core.Module.Tests;

public class ModuleAssemblyLoadContextTests
{
    [Fact]
    public void AssemblyLoadedIntoModuleContext()
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
}
