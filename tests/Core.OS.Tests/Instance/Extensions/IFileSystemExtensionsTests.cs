using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using Core.OS.Instance;
using Core.OS.Instance.Contracts;
using Core.OS.Instance.Extensions;
using Core.OS.Modules;
using Core.OS.Modules.Extensions;
using AwesomeAssertions;
using NSubstitute;
using Sdk.Messaging;
using Sdk.Modules;
using Xunit;

namespace Core.OS.Tests.Instance.Extensions;

public class IFileSystemExtensionsTests
{
    private readonly Serilog.ILogger _logger = Substitute.For<Serilog.ILogger>();

    private readonly InstanceOptions _instanceOptions = new()
    {
        HomeDirectory = "AppData",
        CacheDirectory = "Cache",
        BackupDirectory = "Backup",
        Type = Sdk.Instance.InstanceType.Standalone,
    };

    public sealed class EnsureInstanceIdFile : IFileSystemExtensionsTests
    {
        [Fact]
        public async Task Should_create_default_modules_json_if_it_not_exists()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var expected = fileSystem.GetModuleVersionsFilePath(_instanceOptions);
            var parent = fileSystem.Path.GetDirectoryName(expected);

            fileSystem.AddDirectory(parent);

            // Act
            await fileSystem.EnsureModuleVersionsFile(_instanceOptions, null, _logger);

            // Assert
            fileSystem.File.Exists(expected).Should().BeTrue();
        }

        [Fact]
        public async Task Should_create_default_modules_json_from_seed()
        {
            // Arrange
            var manifestSeed = "seed-manifest.json";
            var manifest = new ModulePackageManifest
            {
                Name = "Test"
            };
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile(manifestSeed, new MockFileData(JsonSerializer.Serialize(manifest, DefaultJsonSerializerSettings.Default)));

            var expected = fileSystem.GetModuleVersionsFilePath(_instanceOptions);
            var parent = fileSystem.Path.GetDirectoryName(expected);

            fileSystem.AddDirectory(parent);

            // Act
            await fileSystem.EnsureModuleVersionsFile(_instanceOptions, manifestSeed, _logger);

            // Assert
            fileSystem.File.Exists(expected).Should().BeTrue();
            var validManifest = JsonSerializer.Deserialize<ModulePackageManifest>(fileSystem.GetFile(expected).TextContents);
            validManifest.Should().BeEquivalentTo(manifest);
        }

        [Fact]
        public async Task Should_do_nothing_if_file_exists()
        {
            // Arrange
            var fileSystem = Substitute.For<IFileSystem>();
            fileSystem.Path.Combine(Arg.Any<string>(), ModuleConstants.ModulesFileName)
                .Returns(ModuleConstants.ModulesFileName);
            fileSystem.File.Exists(ModuleConstants.ModulesFileName).Returns(true);

            // Act
            await fileSystem.EnsureModuleVersionsFile(_instanceOptions, null, _logger);

            // Assert
            fileSystem.FileStream.DidNotReceive().New(Arg.Any<string>(), Arg.Any<FileStreamOptions>());
        }
    }

    public sealed class UseRecoveryMode : IFileSystemExtensionsTests
    {
        private readonly InstanceOptions _recoveryOptions = new()
        {
            HomeDirectory = "AppData",
            CacheDirectory = "Cache",
            BackupDirectory = "Backup",
            Type = Sdk.Instance.InstanceType.Standalone,
            Recovery = new InstanceRecoveryOptions
            {
                MaxStartupAttempts = 3,
                TimespanMinutes = 5,
            }
        };

        [Fact]
        public async Task Should_return_false_if_recovery_options_are_not_set()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            _recoveryOptions.Recovery = null;

            // Act
            var result = await fileSystem.UseRecoveryMode(_recoveryOptions, _logger, CancellationToken.None);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task Should_return_false_on_first_startup()
        {
            // Arrange
            var fileSystem = new MockFileSystem();

            var expected = fileSystem.GetLocalRecoveryFilePath(_recoveryOptions);
            var parent = fileSystem.Path.GetDirectoryName(expected);

            fileSystem.AddDirectory(parent);

            // Act
            var result = await fileSystem.UseRecoveryMode(_recoveryOptions, _logger, CancellationToken.None);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task Should_return_false_last_restart_is_outdated()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var lastStartup = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(_recoveryOptions.Recovery!.TimespanMinutes + 2));
            var recoveryState = new RecoveryState
            {
                LastStartup = lastStartup,
                Startups = 2,
            };

            SetupRecoveryStateFile(fileSystem, _recoveryOptions, recoveryState);

            // Act
            var result = await fileSystem.UseRecoveryMode(_recoveryOptions, _logger, CancellationToken.None);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task Should_return_false_if_max_attempts_not_reached()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var recoveryState = new RecoveryState
            {
                LastStartup = DateTime.UtcNow,
                Startups = 2,
            };

            SetupRecoveryStateFile(fileSystem, _recoveryOptions, recoveryState);

            // Act
            var result = await fileSystem.UseRecoveryMode(_recoveryOptions, _logger, CancellationToken.None);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task Should_return_true_if_max_attempts_reached_within_configured_timespan()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var lastStartup = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(_recoveryOptions.Recovery!.TimespanMinutes - 2));
            var recoveryState = new RecoveryState
            {
                LastStartup = lastStartup,
                Startups = _recoveryOptions.Recovery!.MaxStartupAttempts + 1
            };

            SetupRecoveryStateFile(fileSystem, _recoveryOptions, recoveryState);

            // Act
            var result = await fileSystem.UseRecoveryMode(_recoveryOptions, _logger, CancellationToken.None);

            // Assert
            result.Should().BeTrue();
        }
    }

    public sealed class ReadRecoveryStateReset : IFileSystemExtensionsTests
    {
        [Fact]
        public async Task Should_read_state_from_filesystem()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var recoveryFilePath = fileSystem.GetLocalRecoveryFilePath(_instanceOptions);
            var recoveryState = new RecoveryState
            {
                LastStartup = DateTime.UtcNow,
                Startups = 2,
            };

            SetupRecoveryStateFile(fileSystem, _instanceOptions, recoveryState);

            // Act
            var state = await fileSystem.ReadRecoveryState(recoveryFilePath, _logger);

            // Assert
            state.Should().BeEquivalentTo(recoveryState);
        }

        [Fact]
        public async Task Should_remove_invalid_state_from_filesystem()
        {
            // Arrange
            var fileSystem = Substitute.For<IFileSystem>();
            var recoveryFilePath = fileSystem.GetLocalRecoveryFilePath(_instanceOptions);

            // Act
            _ = await fileSystem.ReadRecoveryState(recoveryFilePath, _logger);

            // Assert
            await fileSystem.File.Received(1).ReadAllTextAsync(recoveryFilePath, Arg.Any<CancellationToken>());
            fileSystem.File.Received(1).Delete(recoveryFilePath);
        }
    }

    public sealed class WriteRecoveryStateReset : IFileSystemExtensionsTests
    {
        [Fact]
        public async Task Should_write_initial_state_to_filesystem()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var recoveryFilePath = fileSystem.GetLocalRecoveryFilePath(_instanceOptions);

            SetupRecoveryStateFolder(fileSystem, recoveryFilePath);

            // Act
            await fileSystem.WriteRecoveryStateReset(recoveryFilePath, CancellationToken.None);

            // Assert
            fileSystem.File.Exists(recoveryFilePath).Should().BeTrue();
            var state = await fileSystem.ReadRecoveryState(recoveryFilePath, _logger);
            Assert.NotNull(state);
            state.Startups.Should().Be(1);
        }

        [Fact]
        public async Task Should_write_init_state_to_filesystem()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var recoveryFilePath = fileSystem.GetLocalRecoveryFilePath(_instanceOptions);
            var recoveryState = new RecoveryState
            {
                LastStartup = DateTime.UtcNow,
                Startups = 2,
            };

            SetupRecoveryStateFile(fileSystem, _instanceOptions, recoveryState);

            // Act
            await fileSystem.WriteRecoveryStateReset(recoveryFilePath, CancellationToken.None);

            // Assert
            fileSystem.File.Exists(recoveryFilePath).Should().BeTrue();
            var state = await fileSystem.ReadRecoveryState(recoveryFilePath, _logger);
            Assert.NotNull(state);
            state.Startups.Should().Be(1);
        }
    }

    private static void SetupRecoveryStateFile(MockFileSystem fileSystem, InstanceOptions options, RecoveryState recoveryState)
    {
        var mockData = new MockFileData(JsonSerializer.Serialize(recoveryState, DefaultJsonSerializerSettings.Default));

        var recoveryFilePath = fileSystem.GetLocalRecoveryFilePath(options);

        SetupRecoveryStateFolder(fileSystem, recoveryFilePath);

        fileSystem.AddFile(recoveryFilePath, mockData);
    }

    private static void SetupRecoveryStateFolder(MockFileSystem fileSystem, string recoveryFilePath)
    {
        var parent = fileSystem.Path.GetDirectoryName(recoveryFilePath);
        fileSystem.AddDirectory(parent);
    }
}
