using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Core.Module.Contracts;
using Core.OS.Extensions;
using Core.OS.Modules;
using Core.OS.Modules.Services;
using Core.Tests.Tools;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Instance;
using Sdk.Modules;
using Sdk.Testing.Backend;
using TestModule.Backend;
using Xunit;

namespace Core.OS.Tests.Modules.Services;

public class ModuleHostBuilderTests
{
    private readonly MockFileSystem _fileSystem = new();

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
            var config = new TestConfig()
                .AddInstanceOptions(type: InstanceType.Standalone)
                .ConfigureModuleLoader()
                .BuildConfiguration();

            var moduleOptions = new Dictionary<string, ModuleOptions>();
            var hostBuilder = new ModuleHostBuilder(_fileSystem, config, moduleOptions);

            // Act
            var moduleHost = await hostBuilder.Build(() => null, CancellationToken.None);

            // Assert
            moduleHost.GetModules().Should().BeEmpty();
        }

        [Fact]
        public async Task Should_load_modules_available_through_suite_context()
        {
            // Arrange
            var context = TestFactory.CreateSuiteContext();

            var config = new TestConfig()
                .AddInstanceOptions(type: InstanceType.Standalone)
                .ConfigureModuleLoader()
                .BuildConfiguration();

            var moduleOptions = new Dictionary<string, ModuleOptions>();
            var hostBuilder = new ModuleHostBuilder(_fileSystem, config, moduleOptions);

            // Act
            var moduleHost = await hostBuilder
                .WithSuiteDependencyContext(context)
                .Build(() => null, CancellationToken.None);

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

            var config = new TestConfig()
                .AddInstanceOptions(type: InstanceType.Standalone)
                .ConfigureModuleLoader()
                .BuildConfiguration();

            var moduleOptions = new Dictionary<string, ModuleOptions>();
            var hostBuilder = new ModuleHostBuilder(_fileSystem, config, moduleOptions);

            SetupBackendModuleMetadataJson();

            // Act
            var moduleHost = await hostBuilder
                .WithSuiteDependencyContext(context)
                .Build(() => null, CancellationToken.None);

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
                .Build(() => null, CancellationToken.None);

            // Assert
            moduleHost.GetContext().Modules.Should().HaveCount(2, "Test.Backend|Client");
        }
    }

    public class WithOptionsSupport : ModuleHostBuilderTests
    {
        [Fact]
        public async Task Should_add_services_and_configuration_source()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();
            var config = new TestConfig()
                .AddInstanceOptions(type: InstanceType.Standalone)
                .AddTestUiHost()
                .ConfigureModuleLoader()
                .AddTestBackendClientModule()
                .BuildConfiguration();

            using var configManager = new ConfigurationManager();
            configManager.AddConfiguration(config);

            var loaderOptions = config.GetModuleLoaderOptions();
            var moduleOptions = config.CreateModuleTestOptions(loaderOptions);
            var hostBuilder = new ModuleHostBuilder(new FileSystem(), config, moduleOptions);

            // Act
            var moduleHost = await hostBuilder
                .WithSuiteDependencyContext()
                .WithOptionsSupport(configManager, serviceCollection)
                .Build(() => null, CancellationToken.None);

            // Assert
            serviceCollection.Should().ContainSingle(s => s.ImplementationInstance is IModuleOptionsStore && s.Lifetime == ServiceLifetime.Singleton);
            configManager.Sources.Should().Contain(k => k is ModuleOptionsSource);
        }

        [Fact]
        public async Task Should_add_option_validation_errors_to_module_error_list()
        {
            // Arrange
            var context = TestFactory.CreateSuiteContext();
            var services = new ServiceCollection();
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
                .WithOptionsSupport(configManager, services)
                .Build(() => null, CancellationToken.None);

            // Assert
            var module = moduleHost.GetManifestModules().First();
            module.Errors.Should().HaveCount(1).And.ContainSingle(e => e.Message == $"Missing required settings: {metadataOption.Key}");
        }

        [Fact]
        public async Task Should_throw_if_suite_context_is_null()
        {
            // Arrange
            var services = new ServiceCollection();
            var config = new TestConfig()
                .AddInstanceOptions(type: InstanceType.Standalone)
                .ConfigureModuleLoader()
                .BuildConfiguration();

            using var configManager = new ConfigurationManager();
            configManager.AddConfiguration(config);
            var moduleOptions = new Dictionary<string, ModuleOptions>();
            var buildTask = new ModuleHostBuilder(_fileSystem, config, moduleOptions)
                .WithOptionsSupport(configManager, services)
                .Build(() => null, CancellationToken.None);

            var act = () => buildTask;

            // Act + Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }

    public class WithSynchronization : ModuleHostBuilderTests
    {
        [Fact(Skip = "Implement after abstraction of synchronizer")]
        public void Should_add_services_and_configuration_source()
        {
            // Arrange

            // Act

            // Assert
        }
    }
}
