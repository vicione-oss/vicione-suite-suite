using System.IO.Abstractions;
using Core.Module;
using Core.Module.Options;
using Core.OS.Instance;
using Core.OS.Modules;
using Core.OS.Modules.Extensions;
using Core.OS.Modules.Services;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Backend.Artifacts;
using Sdk.Backend.Modules;
using Xunit;

namespace Core.OS.Tests.Modules.Extensions;

public class IServiceCollectionExtensionsTests
{
    public sealed class AddModuleManifestProvider
    {
        [Fact]
        public async Task Should_register_expected_services_and_returns_manifest_provider()
        {
            // Arrange
            var services = new ServiceCollection();
            var fileSystem = Substitute.For<IFileSystem>();
            var logger = Substitute.For<Serilog.ILogger>();
            var options = new InstanceOptions()
            {
                Type = Sdk.Instance.InstanceType.Standalone,
                HomeDirectory = "home",
                CacheDirectory = "cache",
                BackupDirectory = "backup"
            };

            // Act
            var result = await services.AddModuleManifestProvider(fileSystem, options, logger);

            // Assert
            using var provider = services.BuildServiceProvider();

            result.Should().NotBeNull();
            provider.GetService<IModuleManifestProvider>().Should().Be(result);
        }
    }

    public sealed class AddModuleServices
    {
        [Fact]
        public void Should_register_all_expected_services()
        {
            // Arrange
            var instanceOptions = new InstanceOptions()
            {
                Type = Sdk.Instance.InstanceType.Standalone,
                HomeDirectory = "home",
                CacheDirectory = "cache",
                BackupDirectory = "backup"
            };

            var moduleOptions = new ArtifactRepositoryOptions
            {
                Sources = [
                    new ArtifactRepositorySource { Endpoint = "http://test.io" }
                ]
            };

            var services = new ServiceCollection()
                .AddSingleton(Substitute.For<IFileSystem>())
                .AddSingleton(Substitute.For<IModuleHost>())
                .AddSingleton(Substitute.For<IModuleOptionsStore>())
                .AddSingleton(Options.Create(moduleOptions))
                .AddSingleton(Substitute.For<ILogger<WorkspaceManagement>>())
                .AddSingleton(Options.Create(instanceOptions)); ;

            // Act
            services.AddModuleServices();
            using var provider = services.BuildServiceProvider();

            // Assert
            provider.GetService<IArtifactRepository>().Should().NotBeNull();
            provider.GetService<IModuleArtifactRepository>().Should().NotBeNull();
            provider.GetService<IModuleArtifactRepository>().Should().BeOfType<ModuleArtifactRepository>();
            provider.GetService<ISuiteArtifactRepository>().Should().NotBeNull();
            provider.GetService<ISuiteArtifactRepository>().Should().BeOfType<SuiteArtifactRepository>();

            provider.GetService<IModuleMetadataCache>().Should().NotBeNull();
            provider.GetService<IModuleMigrator>().Should().NotBeNull();

            provider.GetService<IWorkspaceManagement>().Should().NotBeNull();
            provider.GetService<IWorkspaceProvider<SystemBackendModule>>().Should().NotBeNull();
        }
    }

    public sealed class AddWorkspaceProvider
    {
        [Fact]
        public void Should_register_workspace_provider_for_valid_backend_module()
        {
            // Arrange
            var services = new ServiceCollection()
                .AddSingleton(Substitute.For<IWorkspaceManagement>());

            // Act
            services.AddWorkspaceProvider(typeof(SystemBackendModule));
            using var provider = services.BuildServiceProvider();

            // Assert
            provider.GetService<IWorkspaceProvider<SystemBackendModule>>().Should().NotBeNull();
        }

        [Fact]
        public void Should_throw_if_type_does_not_implement_backend_module()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var act = () => services.AddWorkspaceProvider(typeof(string));

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>()
               .WithParameterName("moduleType");
        }
    }
}
