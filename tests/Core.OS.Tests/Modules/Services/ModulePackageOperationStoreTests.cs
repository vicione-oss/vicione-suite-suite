using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using Core.Module;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.Modules;
using Core.OS.Modules.Extensions;
using Core.OS.Modules.Services;
using Core.Shared.Modules.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdk.Messaging;
using Sdk.Modules;

namespace Core.OS.Tests.Modules.Services;

public class ModulePackageOperationStoreTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly IOptions<InstanceOptions> _options;
    private readonly ILogger<ModulePackageOperationStore> _logger = Substitute.For<ILogger<ModulePackageOperationStore>>();
    private readonly IModulePackageManifestStore _packageStore = Substitute.For<IModulePackageManifestStore>();
    private readonly IModuleArtifactRepository _moduleRepository = Substitute.For<IModuleArtifactRepository>();
    private readonly IServiceProvider _services;

    public ModulePackageOperationStoreTests()
    {
        _packageStore.Load(Arg.Any<CancellationToken>()).Returns(new ModulePackageManifest { Packages = [] });
        _options = Options.Create(new InstanceOptions
        {
            HomeDirectory = "Home",
            BackupDirectory = "Backup",
            CacheDirectory = "Cache",
            Type = Sdk.Instance.InstanceType.Standalone
        });

        _fileSystem = new MockFileSystem();
        var rooted = _fileSystem.GetRootedHomeDirectory(_options.Value);
        _fileSystem.AddDirectory(_fileSystem.Path.Combine(rooted, _options.Value.HomeDirectory));

        _services = new ServiceCollection()
            .AddSingleton<IFileSystem>(_fileSystem)
            .AddSingleton(_options)
            .AddSingleton(_logger)
            .AddSingleton(_packageStore)
            .AddSingleton(_moduleRepository)
            .AddSingleton<IModulePackageOperationStore, ModulePackageOperationStore>()
            .BuildServiceProvider();
    }

    public sealed class EnqueueOperations : ModulePackageOperationStoreTests
    {
        [Fact]
        public async Task Should_create_update_file_with_queued_dependencies()
        {
            // Arrange
            var store = _services.GetRequiredService<IModulePackageOperationStore>();
            var package = new ModuleDependencyPackage() { Name = "TestPackage", Version = "1.0.0" };

            // Act
            _ = await store.EnqueueOperations([new(package, ModulePackageOperationKind.Install)], CancellationToken.None);

            // Assert
            var updateFile = _fileSystem.GetModulePackageOperationsFilePath(_options.Value);
            _fileSystem.File.Exists(updateFile).Should().BeTrue();

            var queued = await DeserializeModulePackageOperations(updateFile);

            queued.Should().HaveCount(1);
            queued.Should().Contain(d => d.Package.Name == package.Name
                && d.Package.Version == package.Version
                && d.OperationKind == ModulePackageOperationKind.Install);
        }

        [Fact]
        public async Task Should_merge_with_existing_queued_operations()
        {
            // Arrange
            var store = _services.GetRequiredService<IModulePackageOperationStore>();
            var queuedPackage = new ModuleDependencyPackage() { Name = "QueuedPackage", Version = "1.0.0" };
            var newPackage = new ModuleDependencyPackage() { Name = "NewPackage", Version = "2.0.0" };
            var otherPackage = new ModuleDependencyPackage() { Name = "OtherPackage", Version = "1.4.2" };

            var updateFile = SetupUpdateQueueFile([
                new ModulePackageOperation(queuedPackage, ModulePackageOperationKind.Install)
            ]);

            var operations = new List<ModulePackageOperation>
            {
                new(newPackage, ModulePackageOperationKind.Install),
                new(otherPackage, ModulePackageOperationKind.Install)
            };

            // Act
            var changes = await store.EnqueueOperations(operations, CancellationToken.None);

            // Assert
            changes.Should().HaveCount(2).And.AllSatisfy(k => k.Action.Should().Be(CrudAction.Created));

            var queued = await DeserializeModulePackageOperations(updateFile);

            queued.Should().HaveCount(3);
            queued.Should().Contain(d => d.Package.Name == queuedPackage.Name
                && d.Package.Version == queuedPackage.Version
                && d.OperationKind == ModulePackageOperationKind.Install);

            queued.Should().Contain(d => d.Package.Name == newPackage.Name
                && d.Package.Version == newPackage.Version
                && d.OperationKind == ModulePackageOperationKind.Install);

            queued.Should().Contain(d => d.Package.Name == otherPackage.Name
                && d.Package.Version == otherPackage.Version
                && d.OperationKind == ModulePackageOperationKind.Install);
        }

        [Fact]
        public async Task Should_overwrite_existing_package_with_same_name()
        {
            // Arrange
            var store = _services.GetRequiredService<IModulePackageOperationStore>();
            var existingPackage = new ModuleDependencyPackage() { Name = "TestPackage", Version = "1.0.0" };
            var overwritePackage = new ModuleDependencyPackage() { Name = "TestPackage", Version = "2.0.0" };

            var updateFile = SetupUpdateQueueFile([
                new ModulePackageOperation(existingPackage, ModulePackageOperationKind.Install)
            ]);

            _moduleRepository.GetModuleMetadata(overwritePackage, Arg.Any<CancellationToken>())
                .Returns(new ModuleMetadata() { Name = overwritePackage.Name, Version = overwritePackage.Version, MinSuiteSdkVersion = "1.0.0", AutonomousMigration = false });

            // Act
            var changes = await store.EnqueueOperations([new(overwritePackage, ModulePackageOperationKind.Install)], CancellationToken.None);

            // Assert
            var queued = await DeserializeModulePackageOperations(updateFile);

            changes.Should().HaveCount(1);
            changes.Should().Contain(d => d.Operation.Package == overwritePackage
                && d.Operation.OperationKind == ModulePackageOperationKind.Install);
        }

        [Fact]
        public async Task Should_not_create_file_when_no_dependencies_to_queue()
        {
            // Arrange
            var store = _services.GetRequiredService<IModulePackageOperationStore>();

            // Act
            var changes = await store.EnqueueOperations([], CancellationToken.None);

            // Assert
            changes.Should().BeEmpty();
            var updateFile = _fileSystem.GetModulePackageOperationsFilePath(_options.Value);
            _fileSystem.File.Exists(updateFile).Should().BeFalse();
        }

        [Fact]
        public async Task Should_remove_redundant_install_package_operations()
        {
            // Arrange
            var store = _services.GetRequiredService<IModulePackageOperationStore>();
            var installedPackage = new ModuleDependencyPackage { Name = "TestPackage", Version = "1.0.0" };

            _packageStore.Load(Arg.Any<CancellationToken>()).Returns(new ModulePackageManifest { Packages = [installedPackage] });

            // Act
            var changes = await store.EnqueueOperations([new(installedPackage, ModulePackageOperationKind.Install)], CancellationToken.None);

            // Assert            
            var updateFile = _fileSystem.GetModulePackageOperationsFilePath(_options.Value);
            _fileSystem.File.Exists(updateFile).Should().BeFalse();

            changes.Should().Contain(k => k.Action == CrudAction.Deleted
                && k.Operation.Package.Name == installedPackage.Name
                && k.Operation.OperationKind == ModulePackageOperationKind.Install);
        }

        [Fact]
        public async Task Should_remove_redundant_uninstall_package_operations()
        {
            // Arrange
            var store = _services.GetRequiredService<IModulePackageOperationStore>();
            var installedPackage = new ModuleDependencyPackage { Name = "TestPackage", Version = "1.0.0" };
            var deletePackage = new ModuleDependencyPackage() { Name = "TestPackage", Version = "2.0.0" };

            _packageStore.Load(Arg.Any<CancellationToken>()).Returns(new ModulePackageManifest { Packages = [installedPackage] });

            var operations = new List<ModulePackageOperation>
            {
                new(deletePackage, ModulePackageOperationKind.Uninstall)
            };

            // Act
            var changes = await store.EnqueueOperations(operations, CancellationToken.None);

            // Assert
            changes.Should().Contain(k => k.Action == CrudAction.Deleted
                && k.Operation.Package == deletePackage);

            var updateFile = _fileSystem.GetModulePackageOperationsFilePath(_options.Value);
            _fileSystem.File.Exists(updateFile).Should().BeFalse();
        }

        [Fact]
        public async Task Should_set_operation_options_when_queued_package_is_updated_to_new_version()
        {
            // Arrange
            var store = _services.GetRequiredService<IModulePackageOperationStore>();
            var previousPackage = new ModuleDependencyPackage { Name = "TestPackage", Version = "1.0.0" };
            var updatedPackage = new ModuleDependencyPackage { Name = "TestPackage", Version = "2.0.0" };

            SetupUpdateQueueFile([new ModulePackageOperation(previousPackage, ModulePackageOperationKind.Install)]);

            _moduleRepository.GetModuleMetadata(Arg.Any<ModuleDependencyPackage>(), Arg.Any<CancellationToken>())
                .Returns(new ModuleMetadata { Name = updatedPackage.Name, Version = updatedPackage.Version, MinSuiteSdkVersion = "1.0.0", AutonomousMigration = true });

            // Act
            var changes = await store.EnqueueOperations([new(updatedPackage, ModulePackageOperationKind.Install)], CancellationToken.None);

            // Assert
            changes.Should().Contain(c => c.Action == CrudAction.Updated
                && c.Operation.Package.Name == updatedPackage.Name);

            var updateFile = _fileSystem.GetModulePackageOperationsFilePath(_options.Value);
            var queued = await DeserializeModulePackageOperations(updateFile);

            var operation = queued!.First(o => o.Package.Name == updatedPackage.Name);
            operation.Options.Should().NotBeNull();
            operation.Options!.AutonomousMigration.Should().BeTrue();
            operation.Options.IsPatchUpdate.Should().BeFalse();
        }

        [Fact]
        public async Task Should_set_operation_options_with_is_patch_when_updating_to_patch_version()
        {
            // Arrange
            var store = _services.GetRequiredService<IModulePackageOperationStore>();
            var previousPackage = new ModuleDependencyPackage { Name = "TestPackage", Version = "1.0.0" };
            var patchPackage = new ModuleDependencyPackage { Name = "TestPackage", Version = "1.0.1" };

            SetupUpdateQueueFile([new ModulePackageOperation(previousPackage, ModulePackageOperationKind.Install)]);

            _moduleRepository.GetModuleMetadata(Arg.Any<ModuleDependencyPackage>(), Arg.Any<CancellationToken>())
                .Returns(new ModuleMetadata { Name = patchPackage.Name, Version = patchPackage.Version, MinSuiteSdkVersion = "1.0.0", AutonomousMigration = false });

            // Act
            _ = await store.EnqueueOperations([new(patchPackage, ModulePackageOperationKind.Install)], CancellationToken.None);

            // Assert
            var updateFile = _fileSystem.GetModulePackageOperationsFilePath(_options.Value);
            var queued = await DeserializeModulePackageOperations(updateFile);

            var operation = queued!.First(o => o.Package.Name == patchPackage.Name);
            operation.Options.Should().NotBeNull();
            operation.Options!.IsPatchUpdate.Should().BeTrue();
            operation.Options.AutonomousMigration.Should().BeFalse();
        }

        [Fact]
        public async Task Should_break_whole_enqueue_process_if_metadata_cannot_be_fetched()
        {
            // Arrange
            var store = _services.GetRequiredService<IModulePackageOperationStore>();
            var previousPackage = new ModuleDependencyPackage { Name = "TestPackage", Version = "1.0.0" };
            var updatedPackage = new ModuleDependencyPackage { Name = "TestPackage", Version = "2.0.0" };

            SetupUpdateQueueFile([new ModulePackageOperation(previousPackage, ModulePackageOperationKind.Install)]);

            _moduleRepository.GetModuleMetadata(Arg.Any<ModuleDependencyPackage>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<ModuleMetadata?>(new InvalidOperationException("Metadata fetch failed")));

            // Act
            var action = () => store.EnqueueOperations([new(updatedPackage, ModulePackageOperationKind.Install)], CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("Metadata fetch failed");
        }
    }

    public class GetEnqueuedOperations : ModulePackageOperationStoreTests
    {
        [Fact]
        public async Task Should_return_queued_operations_when_file_exists()
        {
            // Arrange
            var store = _services.GetRequiredService<IModulePackageOperationStore>();
            var package = new ModuleDependencyPackage() { Name = "TestPackage", Version = "1.0.0" };
            _ = SetupUpdateQueueFile([
                new ModulePackageOperation(package, ModulePackageOperationKind.Install)
            ]);

            // Act
            var operations = await store.GetEnqueuedOperations(CancellationToken.None);

            // Assert
            operations.Should().HaveCount(1);
            operations.Should().Contain(o => o.Package.Name == package.Name
                && o.Package.Version == package.Version
                && o.OperationKind == ModulePackageOperationKind.Install);
        }

        [Fact]
        public async Task Should_return_empty_when_queue_file_does_not_exist()
        {
            // Arrange
            var store = _services.GetRequiredService<IModulePackageOperationStore>();
            var updateFile = _fileSystem.GetModulePackageOperationsFilePath(_options.Value);
            _fileSystem.File.Exists(updateFile).Should().BeFalse();

            // Act
            var operations = await store.GetEnqueuedOperations(CancellationToken.None);

            // Assert
            operations.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_return_empty_when_queue_file_is_invalid()
        {
            // Arrange
            var store = _services.GetRequiredService<IModulePackageOperationStore>();
            var updateFile = _fileSystem.GetModulePackageOperationsFilePath(_options.Value);
            _fileSystem.AddFile(updateFile, new MockFileData("{ invalid json"));

            // Act
            var operations = await store.GetEnqueuedOperations(CancellationToken.None);

            // Assert
            operations.Should().BeEmpty();
        }
    }

    public class ClearEnqueuedOperations : ModulePackageOperationStoreTests
    {
        [Fact]
        public void Should_delete_existing_queue_file()
        {
            // Arrange
            var store = _services.GetRequiredService<IModulePackageOperationStore>();
            var updateFile = SetupUpdateQueueFile([
                new ModulePackageOperation(new ModuleDependencyPackage { Name = "TestPackage", Version = "1.0.0" }, ModulePackageOperationKind.Install)
            ]);

            _fileSystem.File.Exists(updateFile).Should().BeTrue();

            // Act
            store.ClearEnqueuedOperations();

            // Assert
            _fileSystem.File.Exists(updateFile).Should().BeFalse();
        }

        [Fact]
        public void Should_not_throw_when_queue_file_does_not_exist()
        {
            // Arrange
            var store = _services.GetRequiredService<IModulePackageOperationStore>();
            var updateFile = _fileSystem.GetModulePackageOperationsFilePath(_options.Value);
            _fileSystem.File.Exists(updateFile).Should().BeFalse();

            // Act
            var act = () => store.ClearEnqueuedOperations();

            // Assert
            act.Should().NotThrow();
            _fileSystem.File.Exists(updateFile).Should().BeFalse();
        }
    }

    private string SetupUpdateQueueFile(List<ModulePackageOperation> queuedUpdates)
    {
        var updateFile = _fileSystem.GetModulePackageOperationsFilePath(_options.Value);
        var updateJson = JsonSerializer.Serialize(queuedUpdates, DefaultJsonSerializerSettings.Default);
        _fileSystem.AddFile(updateFile, new MockFileData(updateJson));

        return updateFile;
    }

    private async Task<List<ModulePackageOperation>?> DeserializeModulePackageOperations(string jsonFilePath)
    {
        var fileContent = await _fileSystem.File.ReadAllTextAsync(jsonFilePath);
        return JsonSerializer.Deserialize<List<ModulePackageOperation>>(fileContent, DefaultJsonSerializerSettings.Default);
    }
}
