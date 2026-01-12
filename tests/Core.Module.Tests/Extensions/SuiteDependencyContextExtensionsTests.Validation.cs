using Core.Module.Extensions;
using Core.Module.Utils;
using Core.Tests.Tools;
using AwesomeAssertions;
using Sdk.Modules;
using Sdk.Testing.Backend;
using TestSystem.Backend;
using Xunit;

namespace Core.Module.Tests.Extensions;

public partial class SuiteDependencyContextExtensionsTests
{
    public class IsInvalidModule : SuiteDependencyContextExtensionsTests
    {
        [Fact]
        public void Should_be_false_for_modules_without_startup_errors()
        {
            // Arrange
            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS()
                .SetupBlazorServer()
                .SetupPingModule()
                .SetupClusterManagement()
                .Build();

            var invalidModule = suiteContext.Modules
                .First(k => k.AssemblyName == TestDependencyContextBuilder.ClusterManagementBackend);

            // Act + Assert
            suiteContext.IsInvalidModule(invalidModule.AssemblyPath).Should().BeFalse("");
        }

        [Fact]
        public void Should_be_true_for_modules_with_startup_errors()
        {
            // Arrange
            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS()
                .SetupBlazorServer()
                .SetupClusterManagement()
                .Build();

            var invalidModule = suiteContext.Modules
                .First(k => k.AssemblyName == TestDependencyContextBuilder.ClusterManagementBackend);

            // Act + Assert
            suiteContext.IsInvalidModule(invalidModule.AssemblyPath).Should().BeTrue("ClusterManagement depends on Ping ");
        }

        [Fact]
        public void Should_be_true_for_modules_with_dependencies_with_startup_errors()
        {
            // Arrange
            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS()
                .SetupBlazorServer()
                .SetupClusterManagement()
                .SetupDataCollectionWizard()
                .Build();

            var module = suiteContext.Modules
                .First(k => k.AssemblyName == TestDependencyContextBuilder.DataCollectionWizardBackend);

            // Act + Assert
            suiteContext.IsInvalidModule(module.AssemblyPath).Should().BeTrue("DataCollectionWizard depends on ClusterManagement that depends on Ping - Ping is not installed");
        }
    }

    public class ValidateAssemblyModuleType : SuiteDependencyContextExtensionsTests
    {
        [Fact]
        public void Should_validate_modules_by_type()
        {
            // Arrange
            var config = new TestConfig()
                .ConfigureModuleLoader()
                .AddTestUiHost()
                .BuildConfiguration();

            var loaderOptions = config.GetModuleLoaderTestOptions();
            var moduleOptions = config.CreateModuleTestOptions(loaderOptions);
            var suiteContext = new SuiteDependencyContextBuilder()
                .WithCore(typeof(TestSystemModule).Assembly)
                .WithUiHost(loaderOptions, moduleOptions)
                .WithBackendModules(loaderOptions, moduleOptions)
                .WithClientModules(loaderOptions, moduleOptions)
                .Build();

            // Act
            suiteContext.ValidateAssemblyModuleType();

            // Assert
            suiteContext.Modules.SelectMany(k => k.StartupErrors).Should().BeEmpty();
        }

        [Fact]
        public void Should_set_startup_error_if_assembly_does_not_contain_module()
        {
            // Arrange
            var toolsAssembly = typeof(PathHelpers).Assembly;
            var wrongContext = new ModuleDependencyContext(ModuleType.Backend, ModuleHelpers.DllToDepsJson(toolsAssembly.Location), false);

            var coreContext = new SuiteDependencyContextBuilder()
                .WithCore(typeof(TestSystemModule).Assembly)
                .Build();

            var suiteContext = new SuiteDependencyContext(coreContext.Core, null, [wrongContext]);

            // Act
            suiteContext.ValidateAssemblyModuleType();

            // Assert
            suiteContext.Modules
                .First(k => k.AssemblyName == toolsAssembly.GetName().Name)
                .StartupErrors.Should().NotBeEmpty();
        }
    }

    public class ValidateSdkVersion : SuiteDependencyContextExtensionsTests
    {
        [Fact]
        public void Should_validate_matching_sdk_version()
        {
            // Arrange
            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS()
                .SetupClusterManagement()
                .Build();

            // Act
            suiteContext.ValidateSdkVersion();

            // Assert
            suiteContext.Modules
                .First(k => k.AssemblyName == TestDependencyContextBuilder.ClusterManagementBackend)
                .StartupErrors.Should().BeEmpty();
        }

        [Fact]
        public void Should_add_startup_error_for_wrong_sdk_version()
        {
            // Arrange
            var cmVersion = new TestDependencyVersions
            {
                SuiteSdk = "0.17.0"
            };

            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS()
                .SetupClusterManagement(cmVersion)
                .Build();

            // Act
            suiteContext.ValidateSdkVersion();

            // Assert
            suiteContext.Modules
                .First(k => k.AssemblyName == TestDependencyContextBuilder.ClusterManagementBackend)
                .StartupErrors.Should().HaveCount(1);
        }

        [Fact]
        public void Should_handle_sdk_pre_release_versions()
        {
            // Arrange
            var coreVersion = new TestDependencyVersions();
            var cmVersion = new TestDependencyVersions
            {
                SuiteSdk = coreVersion.SuiteSdk + "-ci1343242"
            };

            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS()
                .SetupClusterManagement(cmVersion)
                .Build();

            // Act
            suiteContext.ValidateSdkVersion();

            // Assert
            suiteContext.Modules
                .First(k => k.AssemblyName == TestDependencyContextBuilder.ClusterManagementBackend)
                .StartupErrors.Should().BeEmpty();
        }
    }

    public class ValidateDependencies : SuiteDependencyContextExtensionsTests
    {
        [Fact]
        public void Should_validate_module_dependencies()
        {
            // Arrange
            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS()
                .SetupBlazorServer()
                .SetupDataCollectionWizard()
                .SetupClusterManagement()
                .SetupPingModule()
                .Build();

            // Act
            suiteContext.ValidateDependencies();

            // Assert
            suiteContext.Modules
                .First(k => k.AssemblyName == TestDependencyContextBuilder.ClusterManagementBackend)
                .StartupErrors.Should().BeEmpty();
        }

        [Fact]
        public void Should_add_startup_error_for_missing_dependencies()
        {
            // Arrange
            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS()
                .SetupBlazorServer()
                .SetupDataCollectionWizard()
                .SetupPingModule()
                .Build();

            // Act
            suiteContext.ValidateDependencies();

            // Assert
            suiteContext.Modules
                .First(k => k.AssemblyName == TestDependencyContextBuilder.DataCollectionWizardBackend)
                .StartupErrors.Should().HaveCount(1, "ClusterManagement missing");

            suiteContext.Modules
                .First(k => k.AssemblyName == TestDependencyContextBuilder.DataCollectionWizardClient)
                .StartupErrors.Should().HaveCount(1, "ClusterManagement missing");
        }
    }
}
