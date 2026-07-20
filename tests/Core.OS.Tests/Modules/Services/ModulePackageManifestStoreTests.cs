using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using AwesomeAssertions;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.Modules;
using Core.OS.Modules.Extensions;
using Core.OS.Modules.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Messaging;
using Sdk.Modules;
using Xunit;

namespace Core.OS.Tests.Modules.Services;

public class ModulePackageManifestStoreTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly IOptions<InstanceOptions> _options;
    private readonly ILogger<ModulePackageManifestStore> _logger = Substitute.For<ILogger<ModulePackageManifestStore>>();
    private readonly IServiceProvider _services;

    public ModulePackageManifestStoreTests()
    {
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
            .AddSingleton<IModulePackageManifestStore, ModulePackageManifestStore>()
            .BuildServiceProvider();
    }

    public sealed class Load : ModulePackageManifestStoreTests
    {
        [Fact]
        public async Task Should_load_manifest_from_file()
        {
            // Arrange
            var store = _services.GetRequiredService<IModulePackageManifestStore>();
            var existingPackage = new ModuleDependencyPackage() { Name = "ExistingPackage", Version = "1.0.0" };
            _ = SetupModulePackageManifestFile([existingPackage]);

            // Act
            var manifest = await store.Load(CancellationToken.None);

            // Assert
            manifest.Packages.Should().HaveCount(1);
            manifest.Packages.Should().Contain(d => d.Name == existingPackage.Name && d.Version == existingPackage.Version);
        }

        [Fact]
        public async Task Should_return_empty_manifest_when_file_missing()
        {
            // Arrange
            var store = _services.GetRequiredService<IModulePackageManifestStore>();

            // Act
            var manifest = await store.Load(CancellationToken.None);

            // Assert
            manifest.Packages.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_surface_error_and_not_mask_corrupt_manifest_as_empty()
        {
            // Arrange
            var store = _services.GetRequiredService<IModulePackageManifestStore>();
            var packagesFile = _fileSystem.GetModuleVersionsFilePath(_options.Value);
            _fileSystem.AddFile(packagesFile, new MockFileData("{ this is not valid json"));

            // Act
            var act = () => store.Load(CancellationToken.None);

            // Assert - a corrupt manifest must be surfaced, never silently returned as empty
            await act.Should().ThrowAsync<Exception>();
        }
    }

    public sealed class LoadStatic : ModulePackageManifestStoreTests
    {
        [Fact]
        public async Task Should_load_manifest_from_file()
        {
            // Arrange
            var existingPackage = new ModuleDependencyPackage() { Name = "ExistingPackage", Version = "1.0.0" };
            _ = SetupModulePackageManifestFile([existingPackage]);

            // Act
            var manifest = await ModulePackageManifestStore.Load(_fileSystem, _options.Value, Substitute.For<Serilog.ILogger>(), CancellationToken.None);

            // Assert
            manifest.Packages.Should().HaveCount(1);
            manifest.Packages.Should().Contain(d => d.Name == existingPackage.Name && d.Version == existingPackage.Version);
        }

        [Fact]
        public async Task Should_return_empty_manifest_when_file_missing()
        {
            // Arrange
            var logger = Substitute.For<Serilog.ILogger>();

            // Act
            var manifest = await ModulePackageManifestStore.Load(_fileSystem, _options.Value, logger, CancellationToken.None);

            // Assert
            manifest.Packages.Should().BeEmpty();
        }
    }

    public sealed class Store : ModulePackageManifestStoreTests
    {
        [Fact]
        public async Task Should_write_manifest_to_file()
        {
            // Arrange
            var store = _services.GetRequiredService<IModulePackageManifestStore>();
            var package = new ModuleDependencyPackage() { Name = "StoredPackage", Version = "3.0.0" };
            var packagesFile = _fileSystem.GetModuleVersionsFilePath(_options.Value);
            var manifest = new ModulePackageManifest { Packages = [package] };

            // Act
            await store.Store(manifest, CancellationToken.None);

            // Assert
            _fileSystem.File.Exists(packagesFile).Should().BeTrue();
            var fileContent = await _fileSystem.File.ReadAllTextAsync(packagesFile, TestContext.Current.CancellationToken);
            var storedManifest = JsonSerializer.Deserialize<ModulePackageManifest>(fileContent, DefaultJsonSerializerSettings.Default);

            storedManifest.Should().NotBeNull();
            storedManifest!.Packages.Should().HaveCount(1);
            storedManifest.Packages.Should().Contain(d => d.Name == package.Name && d.Version == package.Version);
        }
    }

    public sealed class StoreStatic : ModulePackageManifestStoreTests
    {
        [Fact]
        public async Task Should_write_manifest_to_file()
        {
            // Arrange
            var package = new ModuleDependencyPackage() { Name = "StoredPackage", Version = "3.0.0" };
            var packagesFile = _fileSystem.GetModuleVersionsFilePath(_options.Value);
            var manifest = new ModulePackageManifest { Packages = [package] };

            // Act
            await ModulePackageManifestStore.Store(manifest, _fileSystem, _options.Value, CancellationToken.None);

            // Assert
            _fileSystem.File.Exists(packagesFile).Should().BeTrue();
            var fileContent = await _fileSystem.File.ReadAllTextAsync(packagesFile, TestContext.Current.CancellationToken);
            var storedManifest = JsonSerializer.Deserialize<ModulePackageManifest>(fileContent, DefaultJsonSerializerSettings.Default);

            storedManifest.Should().NotBeNull();
            storedManifest!.Packages.Should().HaveCount(1);
            storedManifest.Packages.Should().Contain(d => d.Name == package.Name && d.Version == package.Version);
        }
    }

    public sealed class AtomicWrite : ModulePackageManifestStoreTests
    {
        [Fact]
        public async Task Should_leave_previous_file_intact_when_write_fails_mid_write()
        {
            // Arrange - an existing, valid manifest on disk
            var existingPackage = new ModuleDependencyPackage() { Name = "ExistingPackage", Version = "1.0.0" };
            var packagesFile = SetupModulePackageManifestFile([existingPackage]);
            var originalContent = await _fileSystem.File.ReadAllTextAsync(packagesFile, TestContext.Current.CancellationToken);

            // Act - simulate a power-cut mid-write by throwing while writing the new content
            var act = () => _fileSystem.WriteFileAtomic(
                packagesFile,
                _ => throw new IOException("simulated power loss"),
                CancellationToken.None);

            // Assert - the write fails but the previous valid file is untouched
            await act.Should().ThrowAsync<IOException>();
            _fileSystem.File.Exists(packagesFile).Should().BeTrue();
            (await _fileSystem.File.ReadAllTextAsync(packagesFile, TestContext.Current.CancellationToken)).Should().Be(originalContent);
        }
    }

    private string SetupModulePackageManifestFile(List<ModuleDependencyPackage> packages)
    {
        var packagesFile = _fileSystem.GetModuleVersionsFilePath(_options.Value);
        var manifest = new ModulePackageManifest { Packages = packages };
        var manifestJson = JsonSerializer.Serialize(manifest, DefaultJsonSerializerSettings.Default);
        _fileSystem.AddFile(packagesFile, new MockFileData(manifestJson));

        return packagesFile;
    }
}
