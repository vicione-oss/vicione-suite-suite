using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using Core.OS.Instance.Services;
using Core.OS.Tests.Persistence;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Core.OS.Tests.Instance.Services;

public class BackupReaderTests
{
    private ServiceProvider CreateServices()
    {
        var fs = new MockFileSystem();
        fs.AddEmbeddedBackupFile();

        var services = new ServiceCollection();
        services.AddSingleton<IFileSystem>(fs);

        return services.BuildServiceProvider();
    }

    public class GetBackupMetadata : BackupReaderTests
    {
        [Fact]
        public async Task Should_get_metadata_from_backup_file()
        {
            // Arrange
            await using var services = CreateServices();
            var fileSystem = services.GetRequiredService<IFileSystem>();

            // Act
            var metadata = await BackupReader.GetBackupMetadata(fileSystem, TestResources.BackupZip, TestContext.Current.CancellationToken);

            // Assert
            metadata.Should().NotBeNull();
            metadata.Modules.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Should_get_metadata_from_backup_stream()
        {
            // Arrange
            await using var services = CreateServices();
            var fileSystem = services.GetRequiredService<IFileSystem>();
            await using var archiveStream = CreateZipStream(fileSystem);

            // Act
            var metadata = await BackupReader.GetBackupMetadata(archiveStream, TestContext.Current.CancellationToken);

            // Assert
            metadata.Should().NotBeNull();
            metadata.Modules.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Should_get_metadata_from_backup_archive()
        {
            // Arrange
            await using var services = CreateServices();
            var fileSystem = services.GetRequiredService<IFileSystem>();
            await using var zipArchive = CreateZipArchive(fileSystem);

            // Act
            var metadata = await BackupReader.GetBackupMetadata(zipArchive, TestContext.Current.CancellationToken);

            // Assert
            metadata.Should().NotBeNull();
            metadata.Modules.Should().NotBeEmpty();
        }
    }

    public class GetSystemConfiguration : BackupReaderTests
    {
        [Fact]
        public async Task Should_get_system_configuration_from_backup_file()
        {
            // Arrange
            await using var services = CreateServices();
            var fileSystem = services.GetRequiredService<IFileSystem>();

            // Act
            var systemConfiguration = await BackupReader.GetSystemConfiguration(fileSystem, TestResources.BackupZip, TestContext.Current.CancellationToken);

            // Assert
            systemConfiguration.Should().NotBeNull();
        }

        [Fact]
        public async Task Should_get_system_configuration_from_backup_stream()
        {
            // Arrange
            await using var services = CreateServices();
            var fileSystem = services.GetRequiredService<IFileSystem>();
            await using var archiveStream = CreateZipStream(fileSystem);

            // Act
            var systemConfiguration = await BackupReader.GetSystemConfiguration(archiveStream, TestContext.Current.CancellationToken);

            // Assert
            systemConfiguration.Should().NotBeNull();
        }

        [Fact]
        public async Task Should_get_system_configuration_from_backup_archive()
        {
            // Arrange
            await using var services = CreateServices();
            var fileSystem = services.GetRequiredService<IFileSystem>();
            await using var zipArchive = CreateZipArchive(fileSystem);

            // Act
            var systemConfiguration = await BackupReader.GetSystemConfiguration(zipArchive, TestContext.Current.CancellationToken);

            // Assert
            systemConfiguration.Should().NotBeNull();
        }
    }

    public class GetModuleEntries : BackupReaderTests
    {
        [Fact]
        public async Task Should_get_module_entries_from_file()
        {
            // Arrange
            await using var services = CreateServices();
            var fileSystem = services.GetRequiredService<IFileSystem>();

            // Act
            var moduleEntries = BackupReader.GetModuleEntries(fileSystem, TestResources.BackupZip);

            // Assert
            moduleEntries.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Should_get_module_entries_from_stream()
        {
            // Arrange
            await using var services = CreateServices();
            var fileSystem = services.GetRequiredService<IFileSystem>();
            await using var archiveStream = CreateZipStream(fileSystem);

            // Act
            var moduleEntries = BackupReader.GetModuleEntries(archiveStream);

            // Assert
            moduleEntries.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Should_get_module_entries_from_archive()
        {
            // Arrange
            await using var services = CreateServices();
            var fileSystem = services.GetRequiredService<IFileSystem>();
            await using var zipArchive = CreateZipArchive(fileSystem);

            // Act
            var moduleEntries = BackupReader.GetModuleEntries(zipArchive);

            // Assert
            moduleEntries.Should().NotBeEmpty();
        }
    }

    public class ExtractModulesTo : BackupReaderTests
    {
        private const string _destinationPath = "path\\to\\destination";

        [Trait(Traits.Category, Traits.System)]
        [Fact]
        public void Should_extract_all_modules_from_backup_to_destination()
        {
            // Arrange
            using var services = CreateServices();
            var fileSystem = services.GetRequiredService<IFileSystem>();

            // Act
            BackupReader.ExtractModulesTo(fileSystem, TestResources.BackupZip, _destinationPath);

            // Assert
            Directory.Exists(_destinationPath);
        }
    }

    public class ExtractSystemModuleTo : BackupReaderTests
    {
        private const string _destinationPath = "path\\to\\destination";

        [Trait(Traits.Category, Traits.System)]
        [Fact]
        public void Should_extract_system_module_from_backup_to_destination()
        {
            // Arrange
            using var services = CreateServices();
            var fileSystem = services.GetRequiredService<IFileSystem>();

            // Act
            BackupReader.ExtractSystemModuleTo(fileSystem, TestResources.BackupZip, _destinationPath);

            // Assert
            Directory.Exists(_destinationPath);
        }
    }

    private static Stream CreateZipStream(IFileSystem fileSystem)
        => fileSystem.FileStream.New(TestResources.BackupZip, FileMode.Open, FileAccess.Read);

    private static ZipArchive CreateZipArchive(IFileSystem fileSystem)
        => new(CreateZipStream(fileSystem), ZipArchiveMode.Read, leaveOpen: false);
}
