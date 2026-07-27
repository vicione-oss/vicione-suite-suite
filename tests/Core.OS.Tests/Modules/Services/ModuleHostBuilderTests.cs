using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using AwesomeAssertions;
using Core.Module.Contracts;
using Core.OS.Modules;
using Core.OS.Modules.Contracts;
using Core.OS.Modules.Extensions;
using Core.OS.Modules.Services;
using Core.OS.Tests.Extensions;
using Core.Tests.Tools;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Sdk.Instance;
using Sdk.Messaging;
using Sdk.Modules;
using Sdk.Testing.Backend;
using TestModule.Backend;
using Xunit;

namespace Core.OS.Tests.Modules.Services;

public class ModuleHostBuilderTests
{
    private readonly MockFileSystem _fileSystem = new();

    private ModuleHostBuilder SetupModuleHostBuilder()
    {
        var config = new TestConfig()
            .AddInstanceOptions(type: InstanceType.Standalone)
            .ConfigureModuleLoader()
            .BuildConfiguration();

        var moduleOptions = new Dictionary<string, ModuleOptions>();

        return new ModuleHostBuilder(_fileSystem, config, moduleOptions);
    }

    private ModuleMetadata SetupBackendModuleMetadataJson(List<ModuleOptionDeclaration>? moduleOptions = null)
    {
        var metadata = new ModuleMetadata
        {
            Name = TestBackendModule.Id,
            Description = "Generated metadata",
            Version = "1.0.0",
            MinSuiteSdkVersion = "1.0.0",
            Title = nameof(TestBackendModule),
            Options = moduleOptions ?? []
        };

        _fileSystem.SetupModuleMetadataJson(metadata);

        return metadata;
    }

    public class Build : ModuleHostBuilderTests
    {
        [Fact]
        public async Task Should_build_host_without_features()
        {
            // Arrange
            var hostBuilder = SetupModuleHostBuilder();

            // Act
            var moduleHost = await hostBuilder.Build(() => null, TestContext.Current.CancellationToken);

            // Assert
            moduleHost.GetModules().Should().BeEmpty();
        }

        [Fact]
        public async Task Should_load_modules_available_through_suite_context()
        {
            // Arrange
            var context = TestFactory.CreateSuiteContext();
            var hostBuilder = SetupModuleHostBuilder();

            // Act
            var moduleHost = await hostBuilder
                .WithSuiteDependencyContext(context)
                .Build(() => null, TestContext.Current.CancellationToken);

            // Assert
            var modules = moduleHost.GetModules().ToList();
            modules.Should().ContainSingle(k => Equals(k.ModuleKey.ModuleId, TestUiHost.TestUiHostBackend.Id));
            modules.Should().ContainSingle(k => Equals(k.ModuleKey.ModuleId, TestBackendModule.Id));
        }

        [Fact]
        public async Task Should_enrich_modules_with_errors_occurred_on_loading()
        {
            // Arrange
            var context = TestFactory.CreateSuiteContext();
            context.Modules.First().StartupErrors.Add(new InvalidOperationException("Test error"));

            var hostBuilder = SetupModuleHostBuilder();

            SetupBackendModuleMetadataJson();

            // Act
            var moduleHost = await hostBuilder
                .WithSuiteDependencyContext(context)
                .Build(() => null, TestContext.Current.CancellationToken);

            // Assert
            var module = moduleHost.GetManifestModules().First(k => k.ModuleId == TestBackendModule.Id);
            module.Errors.Should().HaveCount(1).And.ContainSingle(e => e.Message == "Test error");
        }
    }

    public class WithSuiteDependencyContext : ModuleHostBuilderTests
    {
        [Fact]
        public async Task Should_build_suite_dependency_context()
        {
            // Arrange
            var config = new TestConfig()
                .AddInstanceOptions(type: InstanceType.Standalone)
                .AddTestUiHost()
                .AddTestBackendClientModule()
                .ConfigureModuleLoader()
                .BuildConfiguration();

            var loaderOptions = config.GetModuleLoaderOptions();
            var moduleOptions = config.CreateModuleTestOptions(loaderOptions);
            var hostBuilder = new ModuleHostBuilder(new FileSystem(), config, moduleOptions);

            // Act
            var moduleHost = await hostBuilder
                .WithSuiteDependencyContext()
                .Build(() => null, TestContext.Current.CancellationToken);

            // Assert
            moduleHost.GetContext().Modules.Should().HaveCount(2, "Test.Backend|Client");
        }
    }

    public class WithOptionsSupport : ModuleHostBuilderTests
    {
        [Fact]
        public async Task Should_add_option_validation_errors_to_module_error_list()
        {
            // Arrange
            var context = TestFactory.CreateSuiteContext();
            var config = new TestConfig()
                .AddInstanceOptions(type: InstanceType.Standalone)
                .ConfigureModuleLoader()
                .BuildConfiguration();

            using var configManager = new ConfigurationManager();
            configManager.AddConfiguration(config);
            var moduleOptions = new Dictionary<string, ModuleOptions>();
            var hostBuilder = new ModuleHostBuilder(_fileSystem, config, moduleOptions);

            var metadataOption = new ModuleOptionDeclaration
            {
                IsRequired = true,
                Key = "MyOption",
                OptionType = ModuleOptionType.Text,
            };

            SetupBackendModuleMetadataJson([metadataOption]);

            // Act
            var moduleHost = await hostBuilder
                .WithSuiteDependencyContext(context)
                .WithOptionsSupport(configManager, Substitute.For<IModuleOptionsStore>())
                .Build(() => null, TestContext.Current.CancellationToken);

            // Assert
            var module = moduleHost.GetManifestModules().First();
            module.Errors.Should().HaveCount(1).And.ContainSingle(e => e.Message == $"Missing required settings: {metadataOption.Key}");
        }

        [Fact]
        public async Task Should_throw_if_suite_context_is_null()
        {
            // Arrange
            var config = new TestConfig()
                .AddInstanceOptions(type: InstanceType.Standalone)
                .ConfigureModuleLoader()
                .BuildConfiguration();

            using var configManager = new ConfigurationManager();
            configManager.AddConfiguration(config);
            var moduleOptions = new Dictionary<string, ModuleOptions>();
            var buildTask = new ModuleHostBuilder(_fileSystem, config, moduleOptions)
                .WithOptionsSupport(configManager, Substitute.For<IModuleOptionsStore>())
                .Build(() => null, TestContext.Current.CancellationToken);

            var act = () => buildTask;

            // Act + Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }

    public class WithSynchronizationResults : ModuleHostBuilderTests
    {
        [Fact]
        public async Task Should_not_add_any_module_errors_when_no_results_are_provided()
        {
            // Arrange
            var hostBuilder = SetupModuleHostBuilder();

            // Act
            var moduleHost = await hostBuilder
                .WithSynchronizationResults(null)
                .Build(() => null, TestContext.Current.CancellationToken);

            // Assert
            moduleHost.GetModules().Should().BeEmpty();
        }

        [Fact]
        public async Task Should_add_error_bundle_for_resolved_result_with_error()
        {
            // Arrange
            const string moduleName = "Unresolvable.Module";
            var hostBuilder = SetupModuleHostBuilder();

            var synchronizationResults = new ModuleSynchronizationResults();
            synchronizationResults.Resolved.Add(new ModuleSynchronizationResult(moduleName)
            {
                Error = new ErrorInfo(1, "Failed to resolve module version"),
            });

            // Act
            var moduleHost = await hostBuilder
                .WithSynchronizationResults(synchronizationResults)
                .Build(() => null, TestContext.Current.CancellationToken);

            // Assert
            var module = moduleHost.GetManifestModules().Should().ContainSingle(k => k.ModuleId == moduleName).Which;
            module.Errors.Should().ContainSingle(e => e.Message == "Failed to resolve module version");
        }

        [Fact]
        public async Task Should_add_error_bundle_for_update_failed_result_with_error()
        {
            // Arrange
            const string moduleName = "UpdateFailed.Module";
            var hostBuilder = SetupModuleHostBuilder();

            var synchronizationResults = new ModuleSynchronizationResults();
            synchronizationResults.UpdateFailed.Add(new ModuleSynchronizationResult(moduleName)
            {
                Error = new ErrorInfo(2, "Failed to download update"),
            });

            // Act
            var moduleHost = await hostBuilder
                .WithSynchronizationResults(synchronizationResults)
                .Build(() => null, TestContext.Current.CancellationToken);

            // Assert
            var module = moduleHost.GetManifestModules().Should().ContainSingle(k => k.ModuleId == moduleName).Which;
            module.Errors.Should().ContainSingle(e => e.Message == "Failed to download update");
        }

        [Fact]
        public async Task Should_not_add_module_error_when_result_has_no_error()
        {
            // Arrange
            const string moduleName = "Healthy.Module";
            var hostBuilder = SetupModuleHostBuilder();

            var synchronizationResults = new ModuleSynchronizationResults();
            synchronizationResults.Resolved.Add(new ModuleSynchronizationResult(moduleName));

            // Act
            var moduleHost = await hostBuilder
                .WithSynchronizationResults(synchronizationResults)
                .Build(() => null, TestContext.Current.CancellationToken);

            // Assert
            moduleHost.GetModules().Should().BeEmpty();
        }

        [Fact]
        public async Task Should_add_error_to_existing_module_bundle_when_module_already_known()
        {
            // Arrange
            var context = TestFactory.CreateSuiteContext();
            var moduleId = context.Modules.First().ModuleId;
            var hostBuilder = SetupModuleHostBuilder();

            SetupBackendModuleMetadataJson();

            var synchronizationResults = new ModuleSynchronizationResults();
            synchronizationResults.Resolved.Add(new ModuleSynchronizationResult(moduleId)
            {
                Error = new ErrorInfo(3, "Synchronization warning"),
            });

            // Act
            var moduleHost = await hostBuilder
                .WithSuiteDependencyContext(context)
                .WithSynchronizationResults(synchronizationResults)
                .Build(() => null, TestContext.Current.CancellationToken);

            // Assert
            var module = moduleHost.GetManifestModules().Should().ContainSingle(k => k.ModuleId == moduleId).Which;
            module.Errors.Should().ContainSingle(e => e.Message == "Synchronization warning");
        }
    }
}
