using System.IO.Abstractions;
using AwesomeAssertions;
using Core.Artifacts;
using Core.Module;
using Core.OS.Instance;
using Core.OS.Modules;
using Core.OS.Modules.Extensions;
using Core.OS.Modules.Services;
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
    public sealed class AddModuleServices
    {
        [Fact]
        public async Task Should_register_all_expected_services()
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
                    new ArtifactRepositorySourceOption { Endpoint = "http://test.io" }
                ]
            };

            var services = new ServiceCollection()
                .AddSingleton(Substitute.For<IArtifactRepositoryOptionsCache>())
                .AddSingleton<IArtifactRepositoryOptionsProvider>(s => s.GetRequiredService<IArtifactRepositoryOptionsCache>())
                .AddSingleton(Substitute.For<IFileSystem>())
                .AddSingleton(Substitute.For<IModuleHost>())
                .AddSingleton(Substitute.For<IModuleOptionsStore>())
                .AddSingleton(Options.Create(moduleOptions))
                .AddSingleton(Substitute.For<ILogger<WorkspaceManagement>>())
                .AddSingleton(Options.Create(instanceOptions));

            // Act
            services.AddModuleServices();
            await using var provider = services.BuildServiceProvider();

            // Assert
            provider.GetService<IArtifactRepository>().Should().NotBeNull();
            provider.GetService<IModuleArtifactRepository>().Should().NotBeNull();
            provider.GetService<IModuleArtifactRepository>().Should().BeOfType<ModuleArtifactRepository>();
            provider.GetService<ISuiteArtifactRepository>().Should().NotBeNull();
            provider.GetService<ISuiteArtifactRepository>().Should().BeOfType<SuiteArtifactRepository>();

            provider.GetService<IModuleArtifactCache>().Should().NotBeNull();
            provider.GetService<IModulePackageManifestStore>().Should().NotBeNull();
            provider.GetService<IModulePackageOperationStore>().Should().NotBeNull();

            provider.GetService<IWorkspaceManagement>().Should().NotBeNull();
            provider.GetService<IWorkspaceProvider<SystemBackendModule>>().Should().NotBeNull();
        }
    }

    public sealed class AddWorkspaceProvider
    {
        [Fact]
        public async Task Should_register_workspace_provider_for_valid_backend_module()
        {
            // Arrange
            var services = new ServiceCollection()
                .AddSingleton(Substitute.For<IWorkspaceManagement>());

            // Act
            services.AddWorkspaceProvider(typeof(SystemBackendModule));
            await using var provider = services.BuildServiceProvider();

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
