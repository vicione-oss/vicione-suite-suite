using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using Core.OS.Instance;
using Core.OS.Instance.Contracts;
using Core.OS.Instance.Extensions;
using Core.OS.Modules;
using Core.OS.Modules.Extensions;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;
using Sdk.Modules;

namespace Core.OS.Tests.Instance.Extensions;

public class IFileSystemExtensionsTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();

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
            await fileSystem.EnsureModuleVersionsFile(_instanceOptions, null, _logger, TestContext.Current.CancellationToken);

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
            await fileSystem.EnsureModuleVersionsFile(_instanceOptions, manifestSeed, _logger, TestContext.Current.CancellationToken);

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
            await fileSystem.EnsureModuleVersionsFile(_instanceOptions, null, _logger, TestContext.Current.CancellationToken);

            // Assert
            fileSystem.FileStream.DidNotReceive().New(Arg.Any<string>(), Arg.Any<FileStreamOptions>());
        }
    }

    public sealed class ReadLocalInstanceId : IFileSystemExtensionsTests
    {
        [Fact]
        public void Should_return_trimmed_instance_id_from_file()
        {
            // Arrange
            var instanceId = Guid.NewGuid();
            var fileSystem = new MockFileSystem();
            var instanceIdFilePath = fileSystem.GetLocalInstanceIdFilePath(_instanceOptions);
            fileSystem.AddFile(instanceIdFilePath, new MockFileData($"{instanceId}\n"));

            // Act
            var result = fileSystem.ReadLocalInstanceId(_instanceOptions);

            // Assert
            result.Should().Be(instanceId.ToString());
        }

        [Fact]
        public void Should_return_null_when_instance_id_file_does_not_exist()
        {
            // Arrange
            var fileSystem = new MockFileSystem();

            // Act
            var result = fileSystem.ReadLocalInstanceId(_instanceOptions);

            // Assert
            result.Should().BeNull();
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
        public async Task Should_return_continue_if_recovery_options_are_not_set()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            _recoveryOptions.Recovery = null;

            // Act
            var result = await fileSystem.UseRecoveryMode(_recoveryOptions, _logger, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(RecoveryDecision.Continue);
        }

        [Fact]
        public async Task Should_return_continue_on_first_startup()
        {
            // Arrange
            var fileSystem = new MockFileSystem();

            var expected = fileSystem.GetLocalRecoveryFilePath(_recoveryOptions);
            var parent = fileSystem.Path.GetDirectoryName(expected);

            fileSystem.AddDirectory(parent);

            // Act
            var result = await fileSystem.UseRecoveryMode(_recoveryOptions, _logger, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(RecoveryDecision.Continue);
        }

        [Fact]
        public async Task Should_return_continue_when_last_restart_is_outdated()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var lastStartup = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(_recoveryOptions.Recovery!.TimespanMinutes + 2));
            var recoveryState = new RecoveryState
            {
                LastStartup = lastStartup,
                Startups = 2,
            };

            SetupRecoveryStateFile(fileSystem, _recoveryOptions, recoveryState);

            // Act
            var result = await fileSystem.UseRecoveryMode(_recoveryOptions, _logger, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(RecoveryDecision.Continue);
        }

        [Fact]
        public async Task Should_return_continue_if_max_attempts_not_reached()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var recoveryState = new RecoveryState
            {
                LastStartup = DateTimeOffset.UtcNow,
                Startups = 2,
            };

            SetupRecoveryStateFile(fileSystem, _recoveryOptions, recoveryState);

            // Act
            var result = await fileSystem.UseRecoveryMode(_recoveryOptions, _logger, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(RecoveryDecision.Continue);
        }

        [Fact]
        public async Task Should_return_apply_recovery_if_max_attempts_reached_within_configured_timespan()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var lastStartup = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(_recoveryOptions.Recovery!.TimespanMinutes - 2));
            var recoveryState = new RecoveryState
            {
                LastStartup = lastStartup,
                Startups = _recoveryOptions.Recovery!.MaxStartupAttempts + 1
            };

            SetupRecoveryStateFile(fileSystem, _recoveryOptions, recoveryState);

            // Act
            var result = await fileSystem.UseRecoveryMode(_recoveryOptions, _logger, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(RecoveryDecision.ApplyRecovery);
        }

        [Fact]
        public async Task Should_mark_recovery_applied_in_state_file_when_recovery_triggers()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var lastStartup = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(_recoveryOptions.Recovery!.TimespanMinutes - 2));
            var recoveryState = new RecoveryState
            {
                LastStartup = lastStartup,
                Startups = _recoveryOptions.Recovery!.MaxStartupAttempts + 1
            };

            SetupRecoveryStateFile(fileSystem, _recoveryOptions, recoveryState);

            // Act
            await fileSystem.UseRecoveryMode(_recoveryOptions, _logger, TestContext.Current.CancellationToken);

            // Assert
            var recoveryFilePath = fileSystem.GetLocalRecoveryFilePath(_recoveryOptions);
            var state = await fileSystem.ReadRecoveryState(recoveryFilePath, _logger, TestContext.Current.CancellationToken);
            state.Should().NotBeNull();
            state!.RecoveryApplied.Should().BeTrue();
            state.Startups.Should().Be(1);
        }

        [Fact]
        public async Task Should_return_recovery_exhausted_when_recovery_was_already_applied_and_threshold_reached_again()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var lastStartup = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(_recoveryOptions.Recovery!.TimespanMinutes - 2));
            var recoveryState = new RecoveryState
            {
                LastStartup = lastStartup,
                Startups = _recoveryOptions.Recovery!.MaxStartupAttempts + 1,
                RecoveryApplied = true
            };

            SetupRecoveryStateFile(fileSystem, _recoveryOptions, recoveryState);

            // Act
            var result = await fileSystem.UseRecoveryMode(_recoveryOptions, _logger, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(RecoveryDecision.RecoveryExhausted);
        }

        [Fact]
        public async Task Should_reset_recovery_applied_flag_when_timespan_elapses()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var lastStartup = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(_recoveryOptions.Recovery!.TimespanMinutes + 2));
            var recoveryState = new RecoveryState
            {
                LastStartup = lastStartup,
                Startups = 2,
                RecoveryApplied = true
            };

            SetupRecoveryStateFile(fileSystem, _recoveryOptions, recoveryState);

            // Act
            var result = await fileSystem.UseRecoveryMode(_recoveryOptions, _logger, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(RecoveryDecision.Continue);
            var recoveryFilePath = fileSystem.GetLocalRecoveryFilePath(_recoveryOptions);
            var state = await fileSystem.ReadRecoveryState(recoveryFilePath, _logger, TestContext.Current.CancellationToken);
            state!.RecoveryApplied.Should().BeFalse();
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
                LastStartup = DateTimeOffset.UtcNow,
                Startups = 2,
            };

            SetupRecoveryStateFile(fileSystem, _instanceOptions, recoveryState);

            // Act
            var state = await fileSystem.ReadRecoveryState(recoveryFilePath, _logger, TestContext.Current.CancellationToken);

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
            _ = await fileSystem.ReadRecoveryState(recoveryFilePath, _logger, TestContext.Current.CancellationToken);

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
            await fileSystem.WriteRecoveryStateReset(recoveryFilePath, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            fileSystem.File.Exists(recoveryFilePath).Should().BeTrue();
            var state = await fileSystem.ReadRecoveryState(recoveryFilePath, _logger, TestContext.Current.CancellationToken);
            state.Should().NotBeNull();
            state!.Startups.Should().Be(1);
            state.RecoveryApplied.Should().BeFalse();
        }

        [Fact]
        public async Task Should_write_init_state_to_filesystem()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var recoveryFilePath = fileSystem.GetLocalRecoveryFilePath(_instanceOptions);
            var recoveryState = new RecoveryState
            {
                LastStartup = DateTimeOffset.UtcNow,
                Startups = 2,
            };

            SetupRecoveryStateFile(fileSystem, _instanceOptions, recoveryState);

            // Act
            await fileSystem.WriteRecoveryStateReset(recoveryFilePath, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            fileSystem.File.Exists(recoveryFilePath).Should().BeTrue();
            var state = await fileSystem.ReadRecoveryState(recoveryFilePath, _logger, TestContext.Current.CancellationToken);
            state.Should().NotBeNull();
            state!.Startups.Should().Be(1);
        }

        [Fact]
        public async Task Should_preserve_recovery_applied_flag_when_specified()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var recoveryFilePath = fileSystem.GetLocalRecoveryFilePath(_instanceOptions);

            SetupRecoveryStateFolder(fileSystem, recoveryFilePath);

            // Act
            await fileSystem.WriteRecoveryStateReset(recoveryFilePath, recoveryApplied: true, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            var state = await fileSystem.ReadRecoveryState(recoveryFilePath, _logger, TestContext.Current.CancellationToken);
            state.Should().NotBeNull();
            state!.Startups.Should().Be(1);
            state.RecoveryApplied.Should().BeTrue();
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
