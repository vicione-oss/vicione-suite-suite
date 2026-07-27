using System.IO.Abstractions.TestingHelpers;
using AwesomeAssertions;
using Core.OS.Instance;
using Core.OS.Instance.Contracts;
using Core.OS.Instance.Extensions;
using Core.OS.Instance.Services;
using Core.OS.Tests.Persistence;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Instance;
using Xunit;

namespace Core.OS.Tests.Extensions;

public class WebApplicationBuilderExtensionsTests
{
    private readonly MockFileSystem _fileSystem = new();
    private readonly ILoggerFactory _loggerFactory = Substitute.For<ILoggerFactory>();
    private readonly string _backupFileName = "2025-05-21_10-08-23.zip";
    private readonly InstanceOptions _instanceOptions;

    public WebApplicationBuilderExtensionsTests()
    {
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());

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
        // NOTE: The step-focused preparation tests were migrated to
        // Core.OS.Tests.Hosting.SuitePreparationPipelineTests.Steps after the pipeline refactor
        // (PrepareSuite is no longer a WebApplicationBuilder extension).

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
            //await RestoreProcessor.HandleSuiteRestore(_fileSystem, instanceOptions, _loggerFactory);

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
