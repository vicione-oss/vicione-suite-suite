using AwesomeAssertions;
using Core.Module.Contracts;
using Core.Module.Options;
using Core.OS.Modules;
using Core.OS.Modules.Extensions;
using Core.OS.Tests.Extensions;
using Core.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sdk.Backend.Extensions;
using Sdk.Modules;
using Sdk.Testing.Backend;
using TestModule.Backend;
using TestUiHost;
using Xunit;

namespace Core.OS.Tests.Modules.Extensions;

public sealed class IConfigurationExtensionsTests
{
    public sealed class GetModuleLoaderOptions
    {
        [Fact]
        public void Should_throw_on_missing_section()
        {
            // Arrange
            var config = new TestConfig().BuildConfiguration();

            // Act + Assert
            Assert.Throws<ConfigurationException>(config.GetModuleLoaderOptions);
        }

        [Fact]
        public void Configuration_should_work()
        {
            // Arrange
            var builder = new ConfigurationBuilder();
            builder.AddCoreAppSettings("Development");

            // Act
            var repositoryOptions = builder.Build().GetModuleLoaderOptions();

            // Assert
            repositoryOptions.UiHost.Should().NotBeNull().And.NotBeEmpty();
        }

        [Fact]
        public void Should_create_api_options_memory_collection()
        {
            // Arrange            
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new Dictionary<string, string?>()
            {
                { "ModuleLoader:AllowInstallation", "true" },
                { "ModuleLoader:DumpMappingFilePath", "/path/to/context-dump" },
                { "ModuleLoader:ManifestSeedPath", "/path/to/manifest-seed.json" },
                { "ModuleLoader:ModulesPath", "/path/to/modules" },
                { "ModuleLoader:ModuleDebugPaths:0", "/path/to/debug-module" },
                { "ModuleLoader:UiHost", "uihost-module-id" },
                { "ModuleLoader:UseTypeValidation", "false" },
            });

            // Act
            var repositoryOptions = builder.Build().GetModuleLoaderOptions();

            // Assert
            repositoryOptions.AllowInstallation.Should().Be(true);
            repositoryOptions.DumpMappingFilePath.Should().Be("/path/to/context-dump");
            repositoryOptions.ManifestSeedPath.Should().Be("/path/to/manifest-seed.json");
            repositoryOptions.ModulesPath.Should().Be("/path/to/modules");
            repositoryOptions.ModuleDebugPaths.Should().ContainSingle(x => x == "/path/to/debug-module");
            repositoryOptions.UiHost.Should().Be("uihost-module-id");
            repositoryOptions.UseTypeValidation.Should().Be(false);
        }
    }

    public sealed class CreateUiHostOptions
    {
        [Fact]
        public void Should_bind_uihost_section_if_set_by_loader_options()
        {
            // Arrange
            var loaderOptions = new ModuleLoaderOptions
            {
                UiHost = TestUiHostBackend.Id,
            };

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>()
                {
                    { $"{TestUiHostBackend.Id}:UseDebugRoot", "true" }
                })
                .Build();

            // Act
            var uiHostOptions = config.CreateUiHostOptions(loaderOptions);

            // Assert
            uiHostOptions.Should().NotBeNull().And.BeOfType<UiHostOptions>();
            uiHostOptions.UseDebugRoot.Should().BeTrue();
        }


        [Fact]
        public void Should_get_custom_ui_host_options_from_configuration()
        {
            // Arrange
            var options = new TestOptions
            {
                Flag = true,
                IntValue = 111,
                StringValue = "SomeVal",
            };

            var config = new TestConfig()
                .AddModuleWithOptions(TestUiHostBackend.Id, options)
                .BuildConfiguration();

            // Act
            var configOptions = config.BindModuleSection<TestOptions>(TestUiHostBackend.Id);

            // Assert
            configOptions.Flag.Should().BeTrue();
            configOptions.IntValue.Should().Be(options.IntValue);
            configOptions.StringValue.Should().Be(options.StringValue);
        }

    }

    public sealed class CreateModuleOptions
    {
        private const string _otherModuleId = "OtherModuleId";
        private readonly ModulePackageManifest _manifest = new()
        {
            Name = "Test",
            Packages = [
                new ModuleDependencyPackage { Name = TestBackendModule.Id, Version = "1.0.0" },
                new ModuleDependencyPackage { Name = _otherModuleId, Version = "1.1.0" },
            ]
        };

        [Fact]
        public void Should_create_options_from_module_sections()
        {
            // Arrange
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new Dictionary<string, string?>()
            {
                { $"{TestBackendModule.Id}:Enable", "false" },
            });

            // Act
            var moduleOptions = builder.Build().CreateModuleOptions(_manifest);

            // Assert
            moduleOptions.Should().ContainKeys(TestBackendModule.Id, _otherModuleId);
            moduleOptions[TestBackendModule.Id].Should().NotBeNull().And.BeOfType<ModuleOptions>();
            moduleOptions[TestBackendModule.Id].Enable.Should().BeFalse();
        }

        [Fact]
        public void Should_add_additional_module_options_from_parameters()
        {
            // Arrange
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new Dictionary<string, string?>()
            {
                { $"{TestBackendModule.Id}:Enable", "false" },
            });

            // Act
            var moduleOptions = builder.Build().CreateModuleOptions(_manifest, ModuleConstants.SampleModuleIds);

            // Assert
            moduleOptions.Should().ContainKey(TestBackendModule.Id);
            moduleOptions.Should().ContainKeys(ModuleConstants.SampleModuleIds);
            moduleOptions[TestBackendModule.Id].Should().NotBeNull().And.BeOfType<ModuleOptions>();
            moduleOptions[TestBackendModule.Id].Enable.Should().BeFalse();
        }

        [Fact]
        public void Should_get_custom_module_options_from_configuration()
        {
            // Arrange
            var customOptions = new TestOptions
            {
                Flag = true,
                StringValue = "TestMe",
                IntValue = 5001,
            };
            var config = new TestConfig()
                .AddModuleWithOptions(TestBackendModule.Id, customOptions)
                .BuildConfiguration();

            // Act
            var configOptions = config.BindModuleSection<TestOptions>(TestBackendModule.Id);

            // Assert
            configOptions.Should().NotBeNull();
            configOptions.Flag.Should().Be(customOptions.Flag);
            configOptions.StringValue.Should().Be(customOptions.StringValue);
            configOptions.IntValue.Should().Be(customOptions.IntValue);
        }

        [Fact]
        public void Should_get_custom_module_options_from_service_provider()
        {
            // Arrange
            var customOptions = new TestOptions
            {
                Flag = true,
                StringValue = "TestMe",
                IntValue = 5001,
            };
            var config = new TestConfig().AddModuleWithOptions(TestBackendModule.Id, customOptions);
            using var serviceProvider = new ServiceCollection()
                .AddConfiguration(config)
                .AddModuleSection<TestOptions>(TestBackendModule.Id.Replace(".", "", StringComparison.Ordinal))
                .BuildServiceProvider();

            // Act
            var serviceOptions = serviceProvider.GetRequiredService<IOptions<TestOptions>>().Value;

            // Assert
            serviceOptions.Should().NotBeNull();
            serviceOptions.Flag.Should().Be(customOptions.Flag);
            serviceOptions.StringValue.Should().Be(customOptions.StringValue);
            serviceOptions.IntValue.Should().Be(customOptions.IntValue);
        }
    }

    public class TestOptions : ModuleOptions
    {
        public bool Flag { get; init; }
        public string? StringValue { get; init; }
        public int IntValue { get; init; }
    }
}
