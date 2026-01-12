using AwesomeAssertions;
using Core.Module;
using Core.OS.Modules;
using Core.OS.Modules.Services;
using Core.Shared.Modules.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Modules;
using TestModule.Backend;
using Xunit;

namespace Core.OS.Tests.Modules.Services;

public sealed class ModuleMigratorTests
{
    private const string TestBackendVersion = "0.10.3";
    private const string TestBackendPatchVersion = "0.10.6";
    private const string TestBackendUpdateVersion = "0.11.6";

    private readonly IModuleMetadataCache _metadataCache = Substitute.For<IModuleMetadataCache>();
    private readonly IModuleArtifactRepository _moduleRepository = Substitute.For<IModuleArtifactRepository>();
    private readonly IWorkspaceManagement _workspaceManagement = Substitute.For<IWorkspaceManagement>();
    private readonly ModuleMetadataBundle _testBackendBundle = new()
    {
        ModuleId = TestBackendModule.GetAssemblyName(),
        Metadata = new()
        {
            Name = TestBackendModule.GetAssemblyName(),
            Version = TestBackendVersion,
            MinSuiteSdkVersion = "0.30.0",
        }
    };
    private readonly ModulePackageManifest _packageManifest = new()
    {
        Packages = [
            new()
            {
                Name = TestBackendModule.GetAssemblyName(),
                Version = TestBackendUpdateVersion,
            },
            new()
            {
                Name = "ViciOne.Suite.ModuleB",
                Version = "1.8.0",
            },
            new()
            {
                Name = "ViciOne.Suite.ModuleC",
                Version = "2.3.1",
            },
        ]
    };

    private void SetupModuleMetadata(string name, string version, bool autonomousMigration = false)
    {
        var targetMetadata = new ModuleMetadata
        {
            Name = name,
            Version = version,
            MinSuiteSdkVersion = _testBackendBundle.Metadata.MinSuiteSdkVersion,
            AutonomousMigration = autonomousMigration,
        };

        _moduleRepository.GetModuleMetadata(Arg.Is<ModuleDependencyPackage>(k => k.Name == name && k.Version == version), Arg.Any<CancellationToken>()).Returns(targetMetadata);
    }

    private ServiceProvider CreateServiceProvider()
        => new ServiceCollection()
            .AddSingleton<ModuleMigrator>()
            .AddSingleton(_metadataCache)
            .AddSingleton(_moduleRepository)
            .AddSingleton(_workspaceManagement)
            .AddSingleton(Substitute.For<ILogger<ModuleMigrator>>())
            .BuildServiceProvider();

    [Fact]
    public async Task Should_not_reset_new_installed_modules()
    {
        // Arrange
        await using var serviceProvider = CreateServiceProvider();
        var migrator = serviceProvider.GetRequiredService<ModuleMigrator>();
        _metadataCache.GetInstalledModuleMetadata(Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        await migrator.PrepareUpdateMigration(_packageManifest);

        // Assert
        _workspaceManagement.Received(0).WriteResetHomeDirectoryFlag(Arg.Any<string>());
        _workspaceManagement.Received(0).WriteResetCacheDirectoryFlag(Arg.Any<string>());
    }

    [Fact]
    public async Task Should_reset_module_workspace_if_update_is_no_patch()
    {
        // Arrange
        await using var serviceProvider = CreateServiceProvider();
        var migrator = serviceProvider.GetRequiredService<ModuleMigrator>();
        SetupModuleMetadata(_testBackendBundle.Metadata.Name, TestBackendUpdateVersion);
        _metadataCache.GetInstalledModuleMetadata(Arg.Any<CancellationToken>())
            .Returns([_testBackendBundle]);

        // Act
        await migrator.PrepareUpdateMigration(_packageManifest);

        // Assert
        _workspaceManagement.Received(1).WriteResetHomeDirectoryFlag(TestBackendModule.GetAssemblyName());
        _workspaceManagement.Received(1).WriteResetCacheDirectoryFlag(TestBackendModule.GetAssemblyName());
    }

    [Fact]
    public async Task Should_not_reset_module_workspace_if_update_is_a_patch()
    {
        // Arrange
        await using var serviceProvider = CreateServiceProvider();
        var migrator = serviceProvider.GetRequiredService<ModuleMigrator>();
        SetupModuleMetadata(_testBackendBundle.Metadata.Name, TestBackendPatchVersion);
        _metadataCache.GetInstalledModuleMetadata(Arg.Any<CancellationToken>())
            .Returns([_testBackendBundle]);

        _packageManifest.Packages.First(k => k.Name == TestBackendModule.GetAssemblyName())
            .Version = TestBackendPatchVersion;

        // Act
        await migrator.PrepareUpdateMigration(_packageManifest);

        // Assert
        _workspaceManagement.Received(0).WriteResetHomeDirectoryFlag(TestBackendModule.GetAssemblyName());
        _workspaceManagement.Received(0).WriteResetCacheDirectoryFlag(TestBackendModule.GetAssemblyName());
    }

    [Fact]
    public async Task Should_not_reset_home_with_autonomous_migration_flag_set()
    {
        // Arrange
        await using var serviceProvider = CreateServiceProvider();
        var migrator = serviceProvider.GetRequiredService<ModuleMigrator>();

        SetupModuleMetadata(_testBackendBundle.Metadata.Name, TestBackendUpdateVersion, true);
        _metadataCache.GetInstalledModuleMetadata(Arg.Any<CancellationToken>())
            .Returns([_testBackendBundle]);

        // Act
        await migrator.PrepareUpdateMigration(_packageManifest);

        // Assert
        _workspaceManagement.Received(0).WriteResetHomeDirectoryFlag(TestBackendModule.GetAssemblyName());
        _workspaceManagement.Received(1).WriteResetCacheDirectoryFlag(TestBackendModule.GetAssemblyName());
    }

    [Fact]
    public async Task Should_throw_if_metadata_of_target_version_is_not_available()
    {
        // Arrange
        await using var serviceProvider = CreateServiceProvider();
        var migrator = serviceProvider.GetRequiredService<ModuleMigrator>();

        _metadataCache.GetInstalledModuleMetadata(Arg.Any<CancellationToken>())
            .Returns([_testBackendBundle]);

        // Act
        var action = () => migrator.PrepareUpdateMigration(_packageManifest);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Can't get metadata of module*");
    }
}
