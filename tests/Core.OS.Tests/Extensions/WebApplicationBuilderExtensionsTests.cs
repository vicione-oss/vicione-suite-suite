using System.IO.Abstractions.TestingHelpers;
using Core.OS.Extensions;
using Core.OS.Instance;
using Core.OS.Instance.Contracts;
using Core.OS.Instance.Extensions;
using Core.OS.Instance.Services;
using Core.OS.Tests.Persistence;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using NSubstitute;
using Sdk.Instance;
using Xunit;

namespace Core.OS.Tests.Extensions;

public class WebApplicationBuilderExtensionsTests
{
    private readonly MockFileSystem _fileSystem = new();
    private readonly Serilog.ILogger _logger = Substitute.For<Serilog.ILogger>();
    private readonly string _backupFileName = "2025-05-21_10-08-23.zip";
    private readonly InstanceOptions _instanceOptions;

    public WebApplicationBuilderExtensionsTests()
    {
        _instanceOptions = new InstanceOptions
        {
            HomeDirectory = _fileSystem.Path.GetFullPath("AppData"),
            CacheDirectory = _fileSystem.Path.GetFullPath("Cache"),
            BackupDirectory = _fileSystem.Path.GetFullPath("Backup"),
            Type = InstanceType.Standalone,
        };

        _fileSystem.AddDirectory(_instanceOptions.CacheDirectory);
        _fileSystem.AddDirectory(_instanceOptions.HomeDirectory);
    }

    public class PrepareSuite : WebApplicationBuilderExtensionsTests
    {

        [Fact]
        public async Task Should_ensure_instance_file_exists()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();

            // Act
            await builder.PrepareSuite(_fileSystem, _instanceOptions, _logger, TestContext.Current.CancellationToken);

            // Assert
            _fileSystem.File.Exists(_fileSystem.GetLocalInstanceIdFilePath(_instanceOptions)).Should().BeTrue();
        }

        [Fact]
        public async Task Should_delete_left_free_device_image()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();
            var cacheDirectory = _fileSystem.GetRootedCacheDirectory(_instanceOptions);
            var deviceImageFile = _fileSystem.Path.Combine(cacheDirectory, Shared.Constants.SystemModuleId, Shared.Constants.DeviceImageFileName);

            SetupTestFiles(_fileSystem, cacheDirectory);
            _fileSystem.AddEmptyFile(deviceImageFile);

            // Act
            await builder.PrepareSuite(_fileSystem, _instanceOptions, _logger, TestContext.Current.CancellationToken);

            // Assert
            _fileSystem.File.Exists(deviceImageFile).Should().BeFalse();
        }

        [Fact]
        public async Task Should_clear_instance_directories_if_reset_file_exists()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();
            var homeDirectory = _fileSystem.GetRootedHomeDirectory(_instanceOptions);
            var cacheDirectory = _fileSystem.GetRootedCacheDirectory(_instanceOptions);
            var backupDirectory = _fileSystem.GetRootedBackupDirectory(_instanceOptions);

            SetupTestFiles(_fileSystem, homeDirectory);
            SetupTestFiles(_fileSystem, cacheDirectory);
            SetupTestFiles(_fileSystem, backupDirectory);

            _fileSystem.WriteResetFile(_instanceOptions);

            // Act
            await builder.PrepareSuite(_fileSystem, _instanceOptions, _logger, TestContext.Current.CancellationToken);

            // Assert
            _fileSystem.Directory.GetDirectories(homeDirectory).Should().BeEmpty();
            _fileSystem.Directory.GetDirectories(cacheDirectory).Should().BeEmpty();
            _fileSystem.Directory.GetFiles(backupDirectory).Should().BeEmpty();
            _fileSystem.ResetFileExists(_instanceOptions).Should().BeFalse();
            _fileSystem.File.Exists(_fileSystem.GetLocalInstanceIdFilePath(_instanceOptions)).Should().BeTrue();
        }

        [Fact]
        public async Task Should_clear_instance_directories_and_artifact_sources_if_reset_file_exists_and_folder_exists()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();
            var homeDirectory = _fileSystem.GetRootedHomeDirectory(_instanceOptions);
            var backupDirectory = _fileSystem.GetRootedBackupDirectory(_instanceOptions);
            var reposSourceFile = _fileSystem.Path.Combine(homeDirectory, "repo-sources.json");

            _fileSystem.AddEmptyFile(reposSourceFile);
            SetupTestFiles(_fileSystem, homeDirectory);
            SetupTestFiles(_fileSystem, backupDirectory);

            _fileSystem.WriteResetFile(_instanceOptions);

            // Act
            await builder.PrepareSuite(_fileSystem, _instanceOptions, _logger, TestContext.Current.CancellationToken);

            // Assert
            _fileSystem.Directory.GetDirectories(homeDirectory).Should().BeEmpty();
            _fileSystem.Directory.GetFiles(backupDirectory).Should().BeEmpty();
            _fileSystem.File.Exists(reposSourceFile).Should().BeFalse();

            _fileSystem.ResetFileExists(_instanceOptions).Should().BeFalse();
            _fileSystem.File.Exists(_fileSystem.GetLocalInstanceIdFilePath(_instanceOptions)).Should().BeTrue();
        }

        [Fact]
        public async Task Should_not_touch_instance_directories_if_reset_file_not_exists()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();
            var homeDirectory = _fileSystem.GetRootedHomeDirectory(_instanceOptions);
            var cacheDirectory = _fileSystem.GetRootedCacheDirectory(_instanceOptions);
            var backupDirectory = _fileSystem.GetRootedBackupDirectory(_instanceOptions);

            SetupTestFiles(_fileSystem, homeDirectory);
            SetupTestFiles(_fileSystem, cacheDirectory);
            SetupTestFiles(_fileSystem, backupDirectory);

            // Act
            await builder.PrepareSuite(_fileSystem, _instanceOptions, _logger, TestContext.Current.CancellationToken);

            // Assert
            _fileSystem.Directory.GetDirectories(homeDirectory).Should().NotBeEmpty();
            _fileSystem.Directory.GetDirectories(cacheDirectory).Should().NotBeEmpty();
            _fileSystem.Directory.GetFiles(backupDirectory).Should().NotBeEmpty();
            _fileSystem.File.Exists(_fileSystem.GetLocalInstanceIdFilePath(_instanceOptions)).Should().BeTrue();
        }

        [Trait(Traits.Category, Traits.System)]
        [Fact]
        public async Task Should_restore_backup()
        {
            // Arrange
            await SetupRestoreTask(_instanceOptions);
            var homeDirectory = _fileSystem.GetRootedHomeDirectory(_instanceOptions);
            var cacheDirectory = _fileSystem.GetRootedCacheDirectory(_instanceOptions);
            var backupDirectory = _fileSystem.GetRootedBackupDirectory(_instanceOptions);
            var backupFilePath = _fileSystem.Path.Combine(backupDirectory, _backupFileName);
            var backupMetadata = await BackupReader.GetBackupMetadata(_fileSystem, backupFilePath, TestContext.Current.CancellationToken);

            SetupTestFiles(_fileSystem, homeDirectory);
            SetupTestFiles(_fileSystem, cacheDirectory);

            // Act
            //await RestoreProcessor.HandleSuiteRestore(_fileSystem, instanceOptions, _logger);

            // Assert
            _fileSystem.Directory.GetDirectories(homeDirectory).Should().BeEmpty();
            _fileSystem.Directory.GetDirectories(cacheDirectory).Should().BeEmpty();

            var added = _fileSystem.Directory.GetDirectories(homeDirectory);
            foreach (var directory in added)
            {
                var name = _fileSystem.Path.GetDirectoryName(directory);
                backupMetadata.Modules.Exists(k => k.Name == name).Should().BeTrue();
            }

            // ZipUtils will restore to real folder
            Directory.Delete(homeDirectory, true);
        }

        private async Task SetupRestoreTask(InstanceOptions instanceOptions)
        {
            var backupFilePath = _fileSystem.Path.Combine(instanceOptions.BackupDirectory, _backupFileName);
            _fileSystem.AddEmbeddedBackupFile(backupFilePath);
            var restoreTask = new RestoreTask(backupFilePath, true, true, DateTimeOffset.Now);
            await _fileSystem.WriteRestoreTask(instanceOptions, restoreTask);
        }

        private static void SetupTestFiles(MockFileSystem fileSystem, string targetPath)
        {
            var folder = fileSystem.Path.Combine(targetPath, "folder");
            fileSystem.AddDirectory(folder);

            var subFolder = fileSystem.Path.Combine(targetPath, "subfolder");
            fileSystem.AddDirectory(subFolder);

            fileSystem.AddEmptyFile(fileSystem.Path.Combine(targetPath, "root.json"));
            fileSystem.AddEmptyFile(fileSystem.Path.Combine(folder, "a.file"));
            fileSystem.AddEmptyFile(fileSystem.Path.Combine(subFolder, "some.file"));
        }
    }
}
