using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.Modules.Extensions;
using Core.OS.Modules.Services;
using Core.Shared.Modules.Contracts;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;
using Sdk.Modules;

namespace Core.OS.Tests.Modules.Services;

public class ModulePackageOperationProcessorTests
{
    private readonly ILoggerFactory _loggerFactory = Substitute.For<ILoggerFactory>();
    private readonly MockFileSystem _fileSystem = new();
    private readonly InstanceOptions _instanceOptions = new()
    {
        HomeDirectory = "Home",
        BackupDirectory = "Backup",
        CacheDirectory = "Cache",
        Type = Sdk.Instance.InstanceType.Standalone
    };


    public sealed class ApplyEnqueuedOperations : ModulePackageOperationProcessorTests
    {
        [Fact]
        public async Task Should_merge_queued_operations_into_packages_file()
        {
            // Arrange
            var existingPackage = new ModuleDependencyPackage() { Name = "ExistingPackage", Version = "1.0.0" };
            var newPackage = new ModuleDependencyPackage() { Name = "NewPackage", Version = "2.0.0" };
            _ = SetupUpdateQueueFile([
                new(newPackage, ModulePackageOperationKind.Install)
            ]);
            SetupModulePackageManifestFile([existingPackage]);

            // Act
            var manifest = await ModulePackageOperationProcessor.ApplyEnqueuedOperations(_fileSystem, _instanceOptions, _loggerFactory, CancellationToken.None);

            // Assert
            manifest.Packages.Should().HaveCount(2);
            manifest.Packages.Should().Contain(d => d.Name == existingPackage.Name && d.Version == existingPackage.Version);
            manifest.Packages.Should().Contain(d => d.Name == newPackage.Name && d.Version == newPackage.Version);
        }

        [Fact]
        public async Task Should_store_updated_package_manifest()
        {
            // Arrange
            var existingPackage = new ModuleDependencyPackage() { Name = "ExistingPackage", Version = "1.0.0" };
            var newPackage = new ModuleDependencyPackage() { Name = "NewPackage", Version = "2.0.0" };
            _ = SetupUpdateQueueFile([
                new(newPackage, ModulePackageOperationKind.Install)
            ]);
            SetupModulePackageManifestFile([existingPackage]);

            // Act
            var manifest = await ModulePackageOperationProcessor.ApplyEnqueuedOperations(_fileSystem, _instanceOptions, _loggerFactory, CancellationToken.None);

            // Assert
            manifest.Packages.Should().HaveCount(2);
        }

        [Fact]
        public async Task Should_overwrite_existing_package_with_queued_version()
        {
            // Arrange
            var existingPackage = new ModuleDependencyPackage() { Name = "TestPackage", Version = "1.0.0" };
            var updatePackage = new ModuleDependencyPackage() { Name = "TestPackage", Version = "2.0.0" };

            SetupModulePackageManifestFile([existingPackage]);
            _ = SetupUpdateQueueFile([
                new(updatePackage, ModulePackageOperationKind.Install)
            ]);


            // Act
            var manifest = await ModulePackageOperationProcessor.ApplyEnqueuedOperations(_fileSystem, _instanceOptions, _loggerFactory, CancellationToken.None);

            // Assert
            manifest.Packages.Should().HaveCount(1);
            manifest.Packages.Should().Contain(d => d.Name == updatePackage.Name && d.Version == updatePackage.Version);
        }

        [Fact]
        public async Task Should_do_nothing_when_no_queued_operations_exist()
        {
            // Arrange
            var existingPackage = new ModuleDependencyPackage() { Name = "TestPackage", Version = "1.0.0" };

            SetupModulePackageManifestFile([existingPackage]);
            _ = SetupUpdateQueueFile([]);

            // Act
            var manifest = await ModulePackageOperationProcessor.ApplyEnqueuedOperations(_fileSystem, _instanceOptions, _loggerFactory, CancellationToken.None);

            // Assert
            manifest.Packages.Should().HaveCount(1);
            manifest.Packages.Should().Contain(d => d.Name == existingPackage.Name && d.Version == existingPackage.Version);
        }

        [Fact]
        public async Task Should_remove_enqueued_operations_after_successfull_apply()
        {
            // Arrange
            var existingPackage = new ModuleDependencyPackage() { Name = "TestPackage", Version = "1.0.0" };
            var updatePackage = new ModuleDependencyPackage() { Name = "TestPackage", Version = "2.0.0" };

            SetupModulePackageManifestFile([existingPackage]);
            _ = SetupUpdateQueueFile([
                new(updatePackage, ModulePackageOperationKind.Install)
            ]);

            // Act
            _ = await ModulePackageOperationProcessor.ApplyEnqueuedOperations(_fileSystem, _instanceOptions, _loggerFactory, CancellationToken.None);

            // Assert
            var updateFile = _fileSystem.GetModulePackageOperationsFilePath(_instanceOptions);
            _fileSystem.File.Exists(updateFile).Should().BeFalse();
        }

        [Fact]
        public async Task Should_remove_dependencies_on_operation_kind_uninstall()
        {
            // Arrange
            var existingPackage = new ModuleDependencyPackage() { Name = "TestPackage", Version = "1.0.0" };
            var toBeRemovedPackage = new ModuleDependencyPackage() { Name = "ToBeRemovedPackage", Version = "1.0.0" };

            SetupModulePackageManifestFile([existingPackage, toBeRemovedPackage]);
            _ = SetupUpdateQueueFile([
                new(toBeRemovedPackage, ModulePackageOperationKind.Uninstall)
            ]);

            // Act
            var manifest = await ModulePackageOperationProcessor.ApplyEnqueuedOperations(_fileSystem, _instanceOptions, _loggerFactory, CancellationToken.None);

            // Assert
            manifest.Packages.Should().HaveCount(1);
            manifest.Packages.Should().Contain(d => d.Name == existingPackage.Name && d.Version == existingPackage.Version);
        }

        [Fact]
        public async Task Should_reset_module_workspace_if_update_is_no_patch()
        {
            // Arrange
            var existingPackage = new ModuleDependencyPackage() { Name = "TestPackage", Version = "1.0.0" };
            var updatePackage = new ModuleDependencyPackage() { Name = "TestPackage", Version = "2.0.0" };

            SetupModulePackageManifestFile([existingPackage]);
            _ = SetupUpdateQueueFile([
                new(updatePackage, ModulePackageOperationKind.Install)
                {
                    Options = new ModulePackageOperationOptions(false, false)
                }
            ]);

            var homeDir = _fileSystem.Path.Combine(_fileSystem.GetRootedHomeDirectory(_instanceOptions), updatePackage.Name);
            var cacheDir = _fileSystem.Path.Combine(_fileSystem.GetRootedCacheDirectory(_instanceOptions), updatePackage.Name);
            _fileSystem.AddDirectory(homeDir);
            _fileSystem.AddDirectory(cacheDir);

            // Act
            _ = await ModulePackageOperationProcessor.ApplyEnqueuedOperations(_fileSystem, _instanceOptions, _loggerFactory, CancellationToken.None);

            // Assert
            _fileSystem.Directory.Exists(homeDir).Should().BeFalse();
            _fileSystem.Directory.Exists(cacheDir).Should().BeFalse();
        }

        [Fact]
        public async Task Should_not_reset_module_workspaces_if_update_is_a_patch()
        {
            // Arrange
            var existingPackage = new ModuleDependencyPackage() { Name = "TestPackage", Version = "1.0.0" };
            var updatePackage = new ModuleDependencyPackage() { Name = "TestPackage", Version = "2.0.0" };

            SetupModulePackageManifestFile([existingPackage]);
            _ = SetupUpdateQueueFile([
                new(updatePackage, ModulePackageOperationKind.Install)
                {
                    Options = new ModulePackageOperationOptions(false, true)
                }
            ]);

            var homeDir = _fileSystem.Path.Combine(_fileSystem.GetRootedHomeDirectory(_instanceOptions), updatePackage.Name);
            var cacheDir = _fileSystem.Path.Combine(_fileSystem.GetRootedCacheDirectory(_instanceOptions), updatePackage.Name);
            _fileSystem.AddDirectory(homeDir);
            _fileSystem.AddDirectory(cacheDir);

            // Act
            _ = await ModulePackageOperationProcessor.ApplyEnqueuedOperations(_fileSystem, _instanceOptions, _loggerFactory, CancellationToken.None);

            // Assert
            _fileSystem.Directory.Exists(homeDir).Should().BeTrue();
            _fileSystem.Directory.Exists(cacheDir).Should().BeTrue();
        }

        [Fact]
        public async Task Should_not_reset_module_home_workspaces_if_autonomous_migration_is_true()
        {
            // Arrange
            var existingPackage = new ModuleDependencyPackage() { Name = "TestPackage", Version = "1.0.0" };
            var updatePackage = new ModuleDependencyPackage() { Name = "TestPackage", Version = "2.0.0" };

            SetupModulePackageManifestFile([existingPackage]);
            _ = SetupUpdateQueueFile([
                new(updatePackage, ModulePackageOperationKind.Install)
                {
                    Options = new ModulePackageOperationOptions(true, false)
                }
            ]);

            var homeDir = _fileSystem.Path.Combine(_fileSystem.GetRootedHomeDirectory(_instanceOptions), updatePackage.Name);
            var cacheDir = _fileSystem.Path.Combine(_fileSystem.GetRootedCacheDirectory(_instanceOptions), updatePackage.Name);
            _fileSystem.AddDirectory(homeDir);
            _fileSystem.AddDirectory(cacheDir);

            // Act
            _ = await ModulePackageOperationProcessor.ApplyEnqueuedOperations(_fileSystem, _instanceOptions, _loggerFactory, CancellationToken.None);

            // Assert
            _fileSystem.Directory.Exists(homeDir).Should().BeTrue();
            _fileSystem.Directory.Exists(cacheDir).Should().BeFalse();
        }
    }

    private string SetupUpdateQueueFile(List<ModulePackageOperation> queuedUpdates)
    {
        var updateFile = _fileSystem.GetModulePackageOperationsFilePath(_instanceOptions);
        var updateJson = JsonSerializer.Serialize(queuedUpdates, DefaultJsonSerializerSettings.Default);
        _fileSystem.AddFile(updateFile, new MockFileData(updateJson));

        return updateFile;
    }

    private string SetupModulePackageManifestFile(List<ModuleDependencyPackage> packages)
    {
        var packagesFile = _fileSystem.GetModuleVersionsFilePath(_instanceOptions);
        var manifest = new ModulePackageManifest { Packages = packages };
        var manifestJson = JsonSerializer.Serialize(manifest, DefaultJsonSerializerSettings.Default);
        _fileSystem.AddFile(packagesFile, new MockFileData(manifestJson));

        return packagesFile;
    }
}
