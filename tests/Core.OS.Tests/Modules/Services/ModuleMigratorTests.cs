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

    private ServiceProvider CreateServiceProvider()
        => new ServiceCollection()
            .AddSingleton<ModuleMigrator>()
            .AddSingleton(_metadataCache)
            .AddSingleton(_workspaceManagement)
            .AddSingleton(Substitute.For<ILogger<ModuleMigrator>>())
            .BuildServiceProvider();

    [Fact]
    public async Task Should_not_reset_new_installed_modules()
    {
        // Arrange
        var migrator = CreateServiceProvider().GetRequiredService<ModuleMigrator>();
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
        var migrator = CreateServiceProvider().GetRequiredService<ModuleMigrator>();
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
        var migrator = CreateServiceProvider().GetRequiredService<ModuleMigrator>();
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
    public async Task Should_respect_module_autonomous_migration()
    {
        // Arrange
        var migrator = CreateServiceProvider().GetRequiredService<ModuleMigrator>();

        _testBackendBundle.Metadata.AutonomousMigration = true;
        _metadataCache.GetInstalledModuleMetadata(Arg.Any<CancellationToken>())
            .Returns([_testBackendBundle]);

        // Act
        await migrator.PrepareUpdateMigration(_packageManifest);

        // Assert
        _workspaceManagement.Received(0).WriteResetHomeDirectoryFlag(TestBackendModule.GetAssemblyName());
        _workspaceManagement.Received(1).WriteResetCacheDirectoryFlag(TestBackendModule.GetAssemblyName());
    }
}
