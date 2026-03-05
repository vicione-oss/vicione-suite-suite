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
    public sealed class GetArtifactRepositoryOptions
    {
        [Fact]
        public void Should_throw_on_missing_section()
        {
            // Arrange
            var config = new TestConfig().BuildConfiguration();

            // Act + Assert
            Assert.Throws<ConfigurationException>(config.GetArtifactRepositoryOptions);
        }

        [Fact]
        public void Should_get_options_from_app_settings()
        {
            // Arrange
            var builder = new ConfigurationBuilder();
            builder.AddCoreAppSettings("Development");

            // Act
            var repositoryOptions = builder.Build().GetArtifactRepositoryOptions();

            // Assert
            repositoryOptions.Sources[0].Endpoint.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void Should_get_backwards_compatible_options_from_memory_collection()
        {
            // Arrange
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new Dictionary<string, string?>()
            {
                { "ModuleApi:Endpoint", "https://system.update.ifm/" },
                { "ModuleApi:UserName", "wildman" },
                { "ModuleApi:Password", "pa$$w0rd" },
                { "ModuleApi:PackageCacheLifetimeMs", "3000" }
            });

            // Act
            var repositoryOptions = builder.Build().GetArtifactRepositoryOptions();

            // Assert
            repositoryOptions.Sources.Should().HaveCount(1);
            repositoryOptions.Sources[0].Endpoint.Should().Be("https://system.update.ifm/");
            repositoryOptions.Sources[0].UserName.Should().Be("wildman");
            repositoryOptions.Sources[0].Password.Should().Be("pa$$w0rd");
            repositoryOptions.PackageCacheLifetimeMs.Should().Be(3000);
        }

        [Fact]
        public void Should_merge_options_from_memory_collection()
        {
            // Arrange
            var migrateEndpoint = "https://system.update.ifm/";
            var currentEndpoint = "https://staging.update.ifm/";

            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new Dictionary<string, string?>()
            {
                { "ModuleApi:Endpoint", migrateEndpoint },
                { "ModuleApi:UserName", "wildman" },
                { "ModuleApi:Password", "pa$$w0rd" },
                { "ModuleApi:PackageCacheLifetimeMs", "3000" },

                { "ArtifactRepository:Sources:0:Endpoint", currentEndpoint },
                { "ArtifactRepository:Sources:0:UserName", "hammerer" },
                { "ArtifactRepository:Sources:0:Password", "d00dle" },
            });

            // Act
            var repositoryOptions = builder.Build().GetArtifactRepositoryOptions();

            // Assert
            repositoryOptions.Sources.Should().HaveCount(2);
            repositoryOptions.PackageCacheLifetimeMs.Should().Be(3000);

            var migratedOption = repositoryOptions.Sources.First(s => s.Endpoint == migrateEndpoint);
            migratedOption.Endpoint.Should().Be(migrateEndpoint);
            migratedOption.UserName.Should().Be("wildman");
            migratedOption.Password.Should().Be("pa$$w0rd");

            var currentOption = repositoryOptions.Sources.First(s => s.Endpoint == currentEndpoint);
            currentOption.Endpoint.Should().Be(currentEndpoint);
            currentOption.UserName.Should().Be("hammerer");
            currentOption.Password.Should().Be("d00dle");
        }
    }

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
                { "ModuleLoader:AllowPreReleases", "true" },
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
            Assert.NotNull(configOptions);
            Assert.Equal(customOptions.Flag, configOptions.Flag);
            Assert.Equal(customOptions.StringValue, configOptions.StringValue);
            Assert.Equal(customOptions.IntValue, configOptions.IntValue);
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
            Assert.NotNull(serviceOptions);
            Assert.Equal(customOptions.Flag, serviceOptions.Flag);
            Assert.Equal(customOptions.StringValue, serviceOptions.StringValue);
            Assert.Equal(customOptions.IntValue, serviceOptions.IntValue);
        }
    }

    public class TestOptions : ModuleOptions
    {
        public bool Flag { get; init; }
        public string? StringValue { get; init; }
        public int IntValue { get; init; }
    }
}
