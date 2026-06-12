using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using AwesomeAssertions;
using Core.Module.Contracts;
using Core.Module.Extensions;
using Core.Module.Options;
using Core.Module.Utils;
using Core.Tests.Tools;
using Microsoft.Extensions.Configuration;
using Sdk.Modules;
using Sdk.Testing.Backend;
using TestModule.Backend;
using TestModule.Client;
using TestSystem.Backend;
using TestUiHost;
using Xunit;

namespace Core.Module.Tests;

public class SuiteDependencyContextBuilderTests
{
    private static IConfiguration CreateConfiguration(bool enableUiHost = true, bool useDebugPaths = true)
        => new TestConfig()
                .ConfigureModuleLoader()
                .AddTestUiHost(enableUiHost, useDebugPaths)
                .BuildConfiguration();

    public class WithCore : SuiteDependencyContextBuilderTests
    {
        [Fact]
        public void Should_create_valid_context()
        {
            // Arrange + Act
            var suiteContext = new SuiteDependencyContextBuilder()
                .WithCore(typeof(TestSystemModule).Assembly)
                .Build();

            // Assert
            suiteContext.Core.Should().NotBeNull();
            suiteContext.UiHost.Should().BeNull();
            suiteContext.Modules.Should().BeEmpty();
        }

        [Fact]
        public void Should_throw_when_core_deps_file_does_not_exist()
        {
            // Arrange
            var builder = new SuiteDependencyContextBuilder()
                .WithCore("this-file-does-not-exist.deps.json");

            // Act + Assert
            var action = () => builder.Build();
            action.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Should_throw_without_core()
        {
            // Arrange + Act
            var buildAction = () => new SuiteDependencyContextBuilder().Build();

            // Assert
            buildAction.Should().Throw<InvalidOperationException>();
        }
    }

    public class WithUiHost : SuiteDependencyContextBuilderTests
    {
        [Fact]
        public void Should_create_valid_context()
        {
            // Arrange
            var config = CreateConfiguration();
            var loaderOptions = config.GetModuleLoaderTestOptions();
            var moduleOptions = config.CreateModuleTestOptions(loaderOptions);
            var builder = new SuiteDependencyContextBuilder()
                .WithCore(typeof(TestSystemModule).Assembly)
                .WithUiHost(loaderOptions, moduleOptions);

            // Act
            var suiteContext = builder.Build();

            // Assert
            AssertModuleContexts(suiteContext);
            suiteContext.Core.Should().NotBeNull();
            suiteContext.UiHost.Should().NotBeNull();
            suiteContext.UiHost.AssemblyName.Should().Be(typeof(TestUiHostBackend).GetTypeAssemblyName());
            suiteContext.Modules.Should().BeEmpty();
            suiteContext.Mappings.Should().NotBeEmpty();
        }

        [Fact]
        public void Should_load_context_with_fixed_ui_host_path()
        {
            // Arrange
            var currentLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location!);
            Assert.NotNull(currentLocation);

            var fileSystem = new MockFileSystem()
                .SetupTestCore()
                .SetupTestUiHost(Path.Combine(currentLocation, "UiHosts"));

            var config = CreateConfiguration(true, false);
            var loaderOptions = config.GetModuleLoaderTestOptions();
            var moduleOptions = config.CreateModuleTestOptions(loaderOptions);
            var builder = new SuiteDependencyContextBuilder()
                .WithCore(typeof(TestSystemModule).Assembly)
                .WithClientModules(loaderOptions, moduleOptions);

            // Act
            var suiteContext = builder.Build(fileSystem);

            // Assert
            AssertModuleContexts(suiteContext);
            suiteContext.Core.Should().NotBeNull();
            suiteContext.UiHost.Should().NotBeNull();
            suiteContext.UiHost.AssemblyName.Should().Be(typeof(TestUiHostBackend).GetTypeAssemblyName());
        }

        [Fact]
        public void Should_throw_on_empty_ui_hosts_path()
        {
            // Arrange
            var config = CreateConfiguration();
            var loaderOptions = new Options.ModuleLoaderOptions() { UiHost = ModuleIdResolver.ResolveId<TestUiHostBackend>(), UiHostsPath = " " };
            var moduleOptions = config.CreateModuleTestOptions(loaderOptions);
            var action = () => new SuiteDependencyContextBuilder()
                .WithCore(typeof(TestSystemModule).Assembly)
                .WithUiHost(loaderOptions, moduleOptions);

            // Act + Assert
            action.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Should_throw_on_missing_ui_host_options()
        {
            // Arrange
            var config = CreateConfiguration();
            var loaderOptions = config.GetModuleLoaderTestOptions();
            var moduleOptions = config.CreateModuleTestOptions(loaderOptions);
            moduleOptions.Clear();

            var action = () => new SuiteDependencyContextBuilder()
                .WithCore(typeof(TestSystemModule).Assembly)
                .WithUiHost(loaderOptions, moduleOptions);

            // Act + Assert
            action.Should().Throw<InvalidOperationException>();
        }
    }

    public class WithBackendModules : SuiteDependencyContextBuilderTests
    {
        [Fact]
        public void Should_add_startup_error_for_invalid_module_with_module_type_validation()
        {
            // Arrange
            var config = new TestConfig()
                .ConfigureModuleLoader()
                .BuildConfiguration();

            var loaderOptions = config.GetModuleLoaderTestOptions();

            var fileSystem = new MockFileSystem()
                .SetupTestCore()
                .SetupTestBackendModule(loaderOptions.ModulesPath!, realDepsJson: true, realDll: false);

            var moduleOptions = new Dictionary<string, ModuleOptions> { [TestBackendModule.Id] = new ModuleOptions { Enable = true } };

            var builder = new SuiteDependencyContextBuilder()
                .WithCore(typeof(TestSystemModule).Assembly)
                .WithBackendModules(loaderOptions, moduleOptions)
                .WithModuleTypeValidation();

            // Act
            var suiteContext = builder.Build(fileSystem);

            // Assert
            var backend = suiteContext.Modules.FirstOrDefault(k => k.AssemblyName == TestBackendModule.GetAssemblyName());
            Assert.NotNull(backend);

            backend!.StartupErrors.Should().HaveCount(1);
        }

        [Fact]
        public void Should_create_valid_context()
        {
            // Arrange
            var config = CreateConfiguration(false);
            var loaderOptions = config.GetModuleLoaderTestOptions();
            var moduleOptions = config.CreateModuleTestOptions(loaderOptions);
            var builder = new SuiteDependencyContextBuilder()
                .WithCore(typeof(TestSystemModule).Assembly)
                .WithBackendModules(loaderOptions, moduleOptions);

            // Act
            var suiteContext = builder.Build();

            // Assert
            AssertModuleContexts(suiteContext);
            suiteContext.Core.Should().NotBeNull();
            suiteContext.UiHost.Should().BeNull();
            suiteContext.Modules.Should().HaveCount(1);
            suiteContext.Modules.Should().ContainSingle(k => k.AssemblyName == typeof(TestBackendModule).GetTypeAssemblyName());
        }
    }

    public class WithClientModules : SuiteDependencyContextBuilderTests
    {
        [Fact]
        public void Should_create_valid_context()
        {
            // Arrange
            var config = CreateConfiguration(true);
            var loaderOptions = config.GetModuleLoaderTestOptions();
            var moduleOptions = config.CreateModuleTestOptions(loaderOptions);
            var builder = new SuiteDependencyContextBuilder()
                .WithCore(typeof(TestSystemModule).Assembly)
                .WithClientModules(loaderOptions, moduleOptions);

            // Act
            var suiteContext = builder.Build();

            // Assert
            AssertModuleContexts(suiteContext);
            suiteContext.Core.Should().NotBeNull();
            suiteContext.UiHost.Should().NotBeNull();
            suiteContext.Modules.Should().HaveCount(1);
            suiteContext.UiHost.AssemblyName.Should().Be(typeof(TestUiHostBackend).GetTypeAssemblyName());
            suiteContext.Modules.Should().ContainSingle(k => k.AssemblyName == typeof(TestClientModule).GetTypeAssemblyName());
        }
    }

    public class WithStartupValidation : SuiteDependencyContextBuilderTests
    {
        [Fact]
        public void Should_create_valid_context()
        {
            // Arrange
            var config = CreateConfiguration(false);
            var loaderOptions = config.GetModuleLoaderTestOptions();
            var moduleOptions = config.CreateModuleTestOptions(loaderOptions);
            var builder = new SuiteDependencyContextBuilder()
                .WithCore(typeof(TestSystemModule).Assembly)
                .WithUiHost(loaderOptions, moduleOptions)
                .WithStartupValidation();

            // Act
            var suiteContext = builder.Build();

            // Assert
            AssertModuleContexts(suiteContext);
            suiteContext.Core.Should().NotBeNull();
            suiteContext.Modules.SelectMany(k => k.StartupErrors).Should().BeEmpty();
        }

        [Fact]
        public void Should_run_startup_validation_when_enabled()
        {
            // Arrange
            var config = new TestConfig()
                .ConfigureModuleLoader()
                .BuildConfiguration();

            var loaderOptions = config.GetModuleLoaderTestOptions();

            var fileSystem = new MockFileSystem()
                .SetupTestCore()
                .SetupTestBackendModule(loaderOptions.ModulesPath!);

            var moduleOptions = new Dictionary<string, ModuleOptions> { [TestBackendModule.Id] = new ModuleOptions { Enable = true } };

            var builder = new SuiteDependencyContextBuilder()
                .WithCore(typeof(TestSystemModule).Assembly)
                .WithBackendModules(loaderOptions, moduleOptions)
                .WithStartupValidation();

            // Act
            var suiteContext = builder.Build(fileSystem);

            // Assert - startup validation should have been executed
            Assert.True(suiteContext.AreDependenciesValidated);
        }
    }

    public class WithMappingDisabled : SuiteDependencyContextBuilderTests
    {
        [Fact]
        public void Should_create_mappings_by_default()
        {
            // Arrange
            var config = CreateConfiguration();
            var loaderOptions = config.GetModuleLoaderTestOptions();
            var moduleOptions = config.CreateModuleTestOptions(loaderOptions);
            var builder = new SuiteDependencyContextBuilder()
                .WithCore(typeof(TestSystemModule).Assembly)
                .WithUiHost(loaderOptions, moduleOptions);

            // Act
            var suiteContext = builder.Build();

            // Assert
            AssertModuleContexts(suiteContext);
            suiteContext.Mappings.Should().NotBeEmpty();
        }

        [Fact]
        public void Should_not_create_mappings_with_mappings_disabled()
        {
            // Arrange
            var config = CreateConfiguration();
            var loaderOptions = config.GetModuleLoaderTestOptions();
            var moduleOptions = config.CreateModuleTestOptions(loaderOptions);
            var builder = new SuiteDependencyContextBuilder()
                .WithCore(typeof(TestSystemModule).Assembly)
                .WithUiHost(loaderOptions, moduleOptions)
                .WithMappingDisabled();

            // Act
            var suiteContext = builder.Build();

            // Assert
            AssertModuleContexts(suiteContext);
            suiteContext.Mappings.Should().BeEmpty();
        }
    }

    public class Build : SuiteDependencyContextBuilderTests
    {
        [Fact]
        public void Different_options_should_create_different_contexts()
        {
            // Arrange
            var config = CreateConfiguration();
            var loaderOptions = config.GetModuleLoaderTestOptions();
            var moduleOptions = config.CreateModuleTestOptions(loaderOptions);
            var suiteBuilder = new SuiteDependencyContextBuilder()
                .WithCore(typeof(TestSystemModule).Assembly)
                .WithUiHost(loaderOptions, moduleOptions)
                .WithMappingDisabled();

            var context1 = suiteBuilder.Build();

            // Act        
            suiteBuilder.WithClientModules(loaderOptions, moduleOptions);
            var context2 = suiteBuilder.Build();

            // Assert
            context1.Should().NotBeEquivalentTo(context2);
        }

        [Fact]
        public void Should_create_equal_contexts_on_multiple_calls()
        {
            // Arrange
            var config = CreateConfiguration(false);
            var loaderOptions = config.GetModuleLoaderTestOptions();
            var moduleOptions = config.CreateModuleTestOptions(loaderOptions);
            var suiteBuilder = new SuiteDependencyContextBuilder()
                .WithCore(typeof(TestSystemModule).Assembly)
                .WithUiHost(loaderOptions, moduleOptions)
                .WithMappingDisabled();

            // Act
            var context1 = suiteBuilder.Build();
            var context2 = suiteBuilder.Build();

            // Assert
            context1.Should().BeEquivalentTo(context2);
        }

    }

    private static void AssertModuleContexts(SuiteDependencyContext context, bool runtimeMode = true)
    {
        Assert.NotEmpty(context.Core.RuntimeLibraries);
        Assert.Empty(context.Core.RedundantLibraries);

        foreach (var moduleContext in context.Modules)
        {
            // modules have no dependencies that are provided by core
            if (moduleContext.RuntimeLibraries.Count > 0)
            {
                moduleContext.RuntimeLibraries.Select(k => k.Name)
                    .Should()
                    .NotBeSubsetOf(context.Core.RuntimeAssets.Select(rtl => rtl.Name));
            }

            // modules have no dependencies that are provided by ui hosts
            if (moduleContext.RuntimeLibraries.Count > 0)
            {
                moduleContext.RuntimeLibraries.Select(k => k.Name)
                    .Should()
                    .NotBeSubsetOf(context.UiHost != null ? context.UiHost?.RedundantLibraries.Select(rtl => rtl.Name) : []);
            }

            // modules have no assets that are provided by ui hosts
            if (moduleContext.RuntimeAssets.Count > 0)
            {
                moduleContext.RuntimeAssets.Select(k => k.Name)
                    .Should()
                    .NotBeSubsetOf(context.UiHost?.RuntimeAssets.Select(rtl => rtl.Name));
            }

            // modules have marked assets redundant that are provided by ui hosts
            if (moduleContext.RedundantAssets.Count > 0)
            {
                moduleContext.RedundantAssets.Select(k => k.Name)
                    .Should()
                    .BeSubsetOf(context.UiHost?.RuntimeAssets.Select(rtl => rtl.Name));
            }

            if (runtimeMode && moduleContext.ModuleType == ModuleType.Backend)
            {
                // assert that modules have only unique runtime libraries
                moduleContext.RuntimeLibraries.Select(k => ModuleHelpers.GetNameVersionKey(k.Name, k.Version))
                    .Should()
                    .NotBeSubsetOf(context.Modules
                        .Where(k => k.AssemblyName != moduleContext.AssemblyName)
                        .SelectMany(dc => dc.RuntimeLibraries.Select(k => ModuleHelpers.GetNameVersionKey(k.Name, k.Version))));
            }
        }
    }

    public class ValidateDependencies : SuiteDependencyContextBuilderTests
    {
        [Fact]
        public void Should_add_startup_errors_for_missing_dependencies()
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

    public class ValidateSdkVersion : SuiteDependencyContextBuilderTests
    {
        [Fact]
        public void Should_add_startup_error_on_version_mismatch()
        {
            // Arrange
            var versions = new TestDependencyVersions { SuiteSdk = "0.17.0" };
            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS()
                .SetupClusterManagement(versions)
                .Build();

            // Act
            suiteContext.ValidateSdkVersion();

            // Assert
            var module = suiteContext.Modules.First(k => k.AssemblyName == TestDependencyContextBuilder.ClusterManagementBackend);

            module.StartupErrors.Should().HaveCount(1);
            module.StartupErrors[0].Message.Should().Contain("Downgrade");
        }
    }
}
