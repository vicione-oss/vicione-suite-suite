using System.IO.Abstractions.TestingHelpers;
using System.Text;
using AwesomeAssertions;
using Core.OS.Instance;
using Core.OS.Instance.Services;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Core.OS.Tests.Instance.Services;

public class BackupStoreTests
{
    private readonly MockFileSystem _fileSystem = new();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly string _backupDirectory = "Backup";

    private BackupStore CreateBackupStore()
    {
        var options = Substitute.For<IOptions<InstanceOptions>>();

        options.Value.Returns(new InstanceOptions
        {
            HomeDirectory = "AppData",
            CacheDirectory = "Cache",
            BackupDirectory = _fileSystem.Path.GetFullPath(_backupDirectory),
            Type = Sdk.Instance.InstanceType.Standalone,
        });
        return new(options, _fileSystem, _timeProvider);
    }

    public sealed class CreateBackupFile : BackupStoreTests
    {
        [Fact]
        public void Should_create_file()
        {
            // Arrange            
            var backupStore = CreateBackupStore();
            _timeProvider.SetUtcNow(new DateTimeOffset(2023, 10, 1, 12, 0, 0, new()));
            var contet = Encoding.UTF8.GetBytes("Hello, World!");

            // Act
            string filename;
            using (var stream = backupStore.CreateBackupFile(out filename))
                stream.Write(contet, 0, contet.Length);

            // Assert
            filename.Should().Be("2023-10-01_12-00-00.zip");
            var path = _fileSystem.Path.Combine(_backupDirectory, filename);
            _fileSystem.File.Exists(path).Should().BeTrue();
            _fileSystem.File.ReadAllBytes(path).Should().BeEquivalentTo(contet);
        }
    }

    public sealed class ReadBackupFile : BackupStoreTests
    {
        [Fact]
        public void Should_read_latest_file()
        {
            // Arrange
            var backupStore = CreateBackupStore();
            _timeProvider.SetUtcNow(new DateTimeOffset(2023, 10, 1, 12, 0, 0, new()));
            using (var stream = backupStore.CreateBackupFile(out var _))
                stream.Write(Encoding.UTF8.GetBytes("1"), 0, 1);

            _timeProvider.SetUtcNow(new DateTimeOffset(2023, 10, 1, 12, 0, 1, new()));
            using (var stream = backupStore.CreateBackupFile(out var _))
                stream.Write(Encoding.UTF8.GetBytes("2"), 0, 1);

            // Act
            using var backupStream = backupStore.ReadBackupFile();
            using StreamReader reader = new(backupStream);

            // Assert
            reader.ReadToEnd().Should().Be("2");
        }

        [Fact]
        public void Should_read_specific_file()
        {
            // Arrange
            var backupStore = CreateBackupStore();
            _timeProvider.SetUtcNow(new DateTimeOffset(2023, 10, 1, 12, 0, 0, new()));
            using (var stream = backupStore.CreateBackupFile(out var _))
                stream.Write(Encoding.UTF8.GetBytes("1"), 0, 1);

            _timeProvider.SetUtcNow(new DateTimeOffset(2023, 10, 1, 12, 0, 1, new()));
            using (var stream = backupStore.CreateBackupFile(out var _))
                stream.Write(Encoding.UTF8.GetBytes("2"), 0, 1);

            // Act
            using var backupStream = backupStore.ReadBackupFile("2023-10-01_12-00-00.zip");
            using StreamReader reader = new(backupStream);

            // Assert
            reader.ReadToEnd().Should().Be("1");
        }
    }

    public sealed class GetBackupFiles : BackupStoreTests
    {
        [Fact]
        public void Should_return_files()
        {
            // Arrange
            var backupStore = CreateBackupStore();

            _timeProvider.SetUtcNow(new DateTimeOffset(2023, 10, 1, 12, 0, 0, new()));
            using (var stream = backupStore.CreateBackupFile(out var _))
                stream.Write(Encoding.UTF8.GetBytes("1"), 0, 1);

            _timeProvider.SetUtcNow(new DateTimeOffset(2023, 10, 1, 12, 0, 1, new()));
            using (var stream = backupStore.CreateBackupFile(out var _))
                stream.Write(Encoding.UTF8.GetBytes("2"), 0, 1);

            // Act
            var backupFiles = backupStore.GetBackupFiles();

            // Assert
            backupFiles.Should().HaveCount(2)
                .And.SatisfyRespectively(
                    e =>
                    {
                        e.Filename.Should().Be("2023-10-01_12-00-00.zip");
                        e.Timestamp.Should().Be(new DateTimeOffset(2023, 10, 1, 12, 0, 0, new()));
                    },
                    e =>
                    {
                        e.Filename.Should().Be("2023-10-01_12-00-01.zip");
                        e.Timestamp.Should().Be(new DateTimeOffset(2023, 10, 1, 12, 0, 1, new()));
                    });
        }
    }

    public sealed class GetLatestBackup : BackupStoreTests
    {
        [Fact]
        public void Should_get_latest_file()
        {
            // Arrange
            var backupStore = CreateBackupStore();
            _timeProvider.SetUtcNow(new DateTimeOffset(2023, 10, 1, 12, 0, 0, new()));
            using (var stream = backupStore.CreateBackupFile(out var _))
                stream.Write(Encoding.UTF8.GetBytes("1"), 0, 1);

            _timeProvider.SetUtcNow(new DateTimeOffset(2023, 10, 1, 12, 0, 1, new()));
            using (var stream = backupStore.CreateBackupFile(out var _))
                stream.Write(Encoding.UTF8.GetBytes("2"), 0, 1);

            // Act
            var fileInfo = backupStore.GetLatestBackup();

            // Assert
            fileInfo.Filename.Should().Be("2023-10-01_12-00-01.zip");
        }

        [Fact]
        public void Should_throw_if_no_backups_available()
        {
            // Arrange
            var backupStore = CreateBackupStore();
            _fileSystem.AddDirectory(_backupDirectory);

            // Act
            var action = () => { var fileInfo = backupStore.GetLatestBackup(); };

            // Assert
            action.Should().Throw<InvalidOperationException>();
        }
    }

    public sealed class DeleteBackupFile : BackupStoreTests
    {
        [Fact]
        public void Should_delete_backup_file()
        {
            // Arrange
            var backupStore = CreateBackupStore();

            _timeProvider.SetUtcNow(new DateTimeOffset(2023, 10, 1, 12, 0, 0, new()));
            using var stream = backupStore.CreateBackupFile(out var filename);
            stream.Write(Encoding.UTF8.GetBytes("1"), 0, 1);
            stream.Close();

            // Act
            backupStore.DeleteBackupFile(filename);

            // Assert
            _fileSystem.FileExists(filename).Should().BeFalse();
        }
    }
}
