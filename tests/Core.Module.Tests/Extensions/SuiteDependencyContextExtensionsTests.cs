using Core.Module.Extensions;
using AwesomeAssertions;
using Sdk.Modules;
using TestModule.Backend;
using TestModule.Client;
using Xunit;

namespace Core.Module.Tests.Extensions;

public partial class SuiteDependencyContextExtensionsTests
{
    public class GetUiModulesAssemblyPathInfos : SuiteDependencyContextExtensionsTests
    {
        [Fact]
        public void Should_return_paths_of_active_client_modules()
        {
            // Arrange
            var suiteContext = TestFactory.CreateSuiteContext(enableBackendModules: false);
            var assemblyName = $"{typeof(TestClientModule).Assembly.GetName().Name}.dll";

            // Act
            var modulePaths = suiteContext.GetUiModulesAssemblyPathInfos();

            // Assert
            modulePaths.Should().ContainSingle(k => k.AssemblyPath.EndsWith(assemblyName, StringComparison.Ordinal));
        }

        [Fact]
        public void Should_be_ordered_by_dependencies()
        {
            // Arrange
            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS()
                .SetupBlazorServer()
                .SetupUiOnlyDependentClientModule()
                .SetupUiOnlyClientModule()
                .Build();

            suiteContext.UseAssemblyMapping();

            // Act
            var modulePaths = suiteContext.GetUiModulesAssemblyPathInfos().Select(k => k.AssemblyPath);

            // Assert
            var expectedOrder = new List<string>
            {
                suiteContext.Modules.First(k => k is { ModuleId: TestDependencyContextBuilder.UiOnlyId, ModuleType: ModuleType.Client }).AssemblyPath,
                suiteContext.Modules.First(k => k is { ModuleId: TestDependencyContextBuilder.UiOnlyDependentModuleId, ModuleType: ModuleType.Client }).AssemblyPath,
            };

            modulePaths.Should().Equal(expectedOrder, "UiOnlyDependent depends on UiOnlyClient");
        }

        [Fact]
        public void Should_not_return_paths_of_uihost_or_backend_modules()
        {
            // Arrange
            var suiteContext = TestFactory.CreateSuiteContext(enableUiModules: false);

            // Act
            var modulePaths = suiteContext.GetUiModulesAssemblyPathInfos();

            // Assert
            modulePaths.Should().BeEmpty();
        }
    }

    public class GetBackendModulesAssemblyPaths : SuiteDependencyContextExtensionsTests
    {
        [Fact]
        public void Should_return_paths_of_active_backend_modules()
        {
            // Arrange
            var suiteContext = TestFactory.CreateSuiteContext(enableUiHost: false, enableUiModules: false);
            var assemblyName = TestBackendModule.GetAssemblyDll();

            // Act
            var modulePaths = suiteContext.GetBackendModulesAssemblyPaths();

            // Assert
            modulePaths.Should().ContainSingle(k => k.EndsWith(assemblyName, StringComparison.Ordinal));
        }

        [Fact]
        public void Should_be_ordered_by_dependencies()
        {
            // Arrange
            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS()
                .SetupBlazorServer()
                .SetupDataCollectionWizard()
                .SetupClusterManagement()
                .SetupPingModule()
                .Build();

            suiteContext.UseAssemblyMapping();

            // Act
            var modulePaths = suiteContext.GetBackendModulesAssemblyPaths();

            // Assert
            var expectedOrder = new List<string>
            {
                suiteContext.Modules.First(k => k is { ModuleId: TestDependencyContextBuilder.PingModuleId, ModuleType: ModuleType.Backend }).AssemblyPath,
                suiteContext.Modules.First(k => k is { ModuleId: TestDependencyContextBuilder.ClusterManagementModuleId, ModuleType: ModuleType.Backend }).AssemblyPath,
                suiteContext.Modules.First(k => k is { ModuleId: TestDependencyContextBuilder.DataCollectionWizardModuleId, ModuleType: ModuleType.Backend }).AssemblyPath,
            };

            modulePaths.Should().Equal(expectedOrder, "DataCollectionWizard depends on ClusterManagement depends on Ping");
        }

        [Fact]
        public void Should_not_return_paths_of_uihost_or_client_modules()
        {
            // Arrange
            var suiteContext = TestFactory.CreateSuiteContext(enableBackendModules: false, enableUiModules: false);

            // Act
            var modulePaths = suiteContext.GetBackendModulesAssemblyPaths();

            // Assert
            modulePaths.Should().BeEmpty();
        }
    }
}
