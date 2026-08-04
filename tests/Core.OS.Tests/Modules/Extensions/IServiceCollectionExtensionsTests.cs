using Core.Artifacts;
using Core.Artifacts.JFrog;
using Core.Module;
using Core.OS.Instance;
using Core.OS.Modules;
using Core.OS.Modules.Extensions;
using Core.OS.Modules.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Backend.Artifacts;
using Sdk.Backend.Modules;

namespace Core.OS.Tests.Modules.Extensions;

public class IServiceCollectionExtensionsTests
{
    public sealed class AddModuleServices
    {
        [Fact]
        public void Should_register_all_expected_services()
        {
            // Arrange
            var services = new ServiceCollection();
            var moduleHost = Substitute.For<IModuleHost>();
            var optionsStore = Substitute.For<IModuleOptionsStore>();

            // Act
            services.AddModuleServices(moduleHost, optionsStore);

            // Assert
            services.Should().ContainSingle(s => s.ServiceType == typeof(IWorkspaceProvider<SystemBackendModule>));
            services.Should().ContainSingle(s => s.ServiceType == typeof(IWorkspaceManagement) && s.ImplementationType == typeof(WorkspaceManagement) && s.Lifetime == ServiceLifetime.Singleton);
            services.Should().ContainSingle(s => s.ServiceType == typeof(IModuleHost) && s.ImplementationInstance == moduleHost);
            services.Should().ContainSingle(s => s.ServiceType == typeof(IModuleOptionsStore) && s.ImplementationInstance == optionsStore);
            services.Should().ContainSingle(s => s.ServiceType == typeof(IModuleArtifactCache) && s.ImplementationType == typeof(ModuleArtifactCache) && s.Lifetime == ServiceLifetime.Singleton);
            services.Should().ContainSingle(s => s.ServiceType == typeof(IModuleMetadataProvider) && s.ImplementationType == typeof(ModuleMetadataProvider) && s.Lifetime == ServiceLifetime.Transient);
            services.Should().ContainSingle(s => s.ServiceType == typeof(IModulePackageManifestStore) && s.ImplementationType == typeof(ModulePackageManifestStore) && s.Lifetime == ServiceLifetime.Singleton);
            services.Should().ContainSingle(s => s.ServiceType == typeof(IModulePackageOperationStore) && s.ImplementationType == typeof(ModulePackageOperationStore) && s.Lifetime == ServiceLifetime.Singleton);
        }
    }

    public sealed class AddModuleArtifactQueryApi
    {
        [Fact]
        public void Should_register_all_expected_services()
        {
            // Arrange
            var services = new ServiceCollection();
            var optionsCache = Substitute.For<IArtifactRepositoryOptionsCache>();

            // Act
            services.AddModuleArtifactQueryApi(optionsCache);

            // Assert
            services.Should().ContainSingle(s => s.ServiceType == typeof(IArtifactRepositoryOptionsCache) && s.ImplementationInstance == optionsCache);
            services.Should().ContainSingle(s => s.ServiceType == typeof(IArtifactRepositoryOptionsProvider) && s.Lifetime == ServiceLifetime.Transient);
            services.Should().ContainSingle(s => s.ServiceType == typeof(IArtifactRepository) && s.ImplementationType == typeof(JFrogArtifactRepository) && s.Lifetime == ServiceLifetime.Transient);
            services.Should().ContainSingle(s => s.ServiceType == typeof(IModuleArtifactRepository) && s.ImplementationType == typeof(ModuleArtifactRepository) && s.Lifetime == ServiceLifetime.Transient);
            services.Should().ContainSingle(s => s.ServiceType == typeof(ISuiteArtifactRepository) && s.ImplementationType == typeof(SuiteArtifactRepository) && s.Lifetime == ServiceLifetime.Transient);
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
