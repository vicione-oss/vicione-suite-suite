using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using System.Text.Json;
using Core.Module.Utils;
using Core.OS.Instance;
using Core.OS.Instance.Services;
using Core.OS.Modules;
using Core.OS.Modules.Extensions;
using Sdk.Messaging;
using Sdk.Modules;
using Microsoft.Extensions.Logging;

namespace Core.OS.Tests.Modules.Extensions;

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

    public class EnsureModuleVersionsFile : IFileSystemExtensionsTests
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
            var manifest = new ModulePackageManifest { Name = "Test" };
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

    public class EvaluateLocalVersionString
    {
        private readonly MockFileSystem _fileSystem = new();
        private readonly Assembly? _coreAssembly = Assembly.GetAssembly(typeof(LocalInstanceInformationProvider));


        [Fact]
        public void Should_parse_version_from_json()
        {
            // Arrange
            var appPath = _fileSystem.Path.GetDirectoryName(_coreAssembly!.Location);
            var dataPath = _fileSystem.Path.Combine(appPath!, "version.json");

            _fileSystem.AddFile(dataPath, "{\"Version\":\"v0.38.1 (40454a3e)\"}");

            // Act
            var version = _fileSystem.EvaluateLocalVersionString(out var branchName);

            // Assert
            branchName.Should().BeNull();
            version.Should().Be("0.38.1");
        }

        [Fact]
        public void Should_fallback_to_assembly_version_if_file_is_malformed()
        {
            // Arrange
            var appPath = _fileSystem.Path.GetDirectoryName(_coreAssembly!.Location);
            var dataPath = _fileSystem.Path.Combine(appPath!, "version.json");

            _fileSystem.AddFile(dataPath, "{'\\}}");

            // Act
            var version = _fileSystem.EvaluateLocalVersionString(out var branchName);

            // Assert
            branchName.Should().BeNull();
            version.Should().Be(ModuleHelpers.GetNormalizedVersion(_coreAssembly!));
        }

        [Fact]
        public void Should_fallback_to_assembly_version_if_branch_name_is_set()
        {
            // Arrange
            var appPath = _fileSystem.Path.GetDirectoryName(_coreAssembly!.Location);
            var dataPath = _fileSystem.Path.Combine(appPath!, "version.json");

            _fileSystem.AddFile(dataPath, "{\"Version\":\"4c3b3a3d-1276-directory-build-props\"}");

            // Act
            var version = _fileSystem.EvaluateLocalVersionString(out var branchName);

            // Assert
            branchName.Should().Be("4c3b3a3d-1276-directory-build-props");
            version.Should().Be(ModuleHelpers.GetNormalizedVersion(_coreAssembly!));
        }
    }

    /// <summary>
    /// Uses a real file system because the atomic write relies on file move semantics and unix
    /// file permissions which are not modelled by the in-memory file system.
    /// </summary>
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility",
        Justification = "Unix file mode APIs are guarded by SkipUnless(IsLinux) which aborts the test before they are reached on other platforms.")]
    public sealed class WriteFileAtomic : IDisposable
    {
        private const string SkipReason = "Unix file permissions are only available on linux.";

        private readonly IFileSystem _fileSystem = new FileSystem();
        private readonly string _rootPath = Path.Combine(Path.GetTempPath(), $"atomic-write-{Guid.NewGuid():N}");

        public static bool IsLinux => OperatingSystem.IsLinux();

        public WriteFileAtomic() => Directory.CreateDirectory(_rootPath);

        public void Dispose()
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);

            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task Should_write_content_if_file_does_not_exist()
        {
            // Arrange
            var filePath = Path.Combine(_rootPath, "content.json");

            // Act
            await _fileSystem.WriteFileAtomic(filePath, WriteText("created"), TestContext.Current.CancellationToken);

            // Assert
            (await _fileSystem.File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken)).Should().Be("created");
        }

        [Fact]
        public async Task Should_create_missing_target_directory()
        {
            // Arrange
            var filePath = Path.Combine(_rootPath, "nested", "sub", "content.json");

            // Act
            await _fileSystem.WriteFileAtomic(filePath, WriteText("created"), TestContext.Current.CancellationToken);

            // Assert
            _fileSystem.File.Exists(filePath).Should().BeTrue();
        }

        [Fact]
        public async Task Should_replace_content_of_existing_file()
        {
            // Arrange
            var filePath = Path.Combine(_rootPath, "content.json");
            await _fileSystem.File.WriteAllTextAsync(filePath, "a much longer previous content", TestContext.Current.CancellationToken);

            // Act
            await _fileSystem.WriteFileAtomic(filePath, WriteText("new"), TestContext.Current.CancellationToken);

            // Assert
            (await _fileSystem.File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken)).Should().Be("new");
        }

        [Fact]
        public async Task Should_not_leave_a_temp_file_behind_on_success()
        {
            // Arrange
            var filePath = Path.Combine(_rootPath, "content.json");

            // Act
            await _fileSystem.WriteFileAtomic(filePath, WriteText("created"), TestContext.Current.CancellationToken);

            // Assert
            _fileSystem.File.Exists(filePath + ".tmp").Should().BeFalse();
        }

        [Fact]
        public async Task Should_not_leave_a_temp_file_behind_if_write_fails()
        {
            // Arrange
            var filePath = Path.Combine(_rootPath, "content.json");

            // Act
            var act = () => _fileSystem.WriteFileAtomic(
                filePath,
                _ => throw new IOException("simulated power loss"),
                TestContext.Current.CancellationToken);

            // Assert
            await act.Should().ThrowAsync<IOException>();
            _fileSystem.File.Exists(filePath + ".tmp").Should().BeFalse();
        }

        [Fact]
        public async Task Should_not_leave_a_temp_file_behind_if_move_fails()
        {
            // Arrange - an existing directory as target makes the move fail after the content was written
            var filePath = Path.Combine(_rootPath, "content.json");
            _fileSystem.Directory.CreateDirectory(filePath);

            // Act
            var act = () => _fileSystem.WriteFileAtomic(filePath, WriteText("created"), TestContext.Current.CancellationToken);

            // Assert - the concrete type differs per platform (IOException on linux, UnauthorizedAccessException on windows)
            await act.Should().ThrowAsync<SystemException>();
            _fileSystem.File.Exists(filePath + ".tmp").Should().BeFalse();
        }

        [Fact]
        public async Task Should_keep_previous_file_intact_if_write_fails()
        {
            // Arrange
            var filePath = Path.Combine(_rootPath, "content.json");
            await _fileSystem.File.WriteAllTextAsync(filePath, "previous", TestContext.Current.CancellationToken);

            // Act
            var act = () => _fileSystem.WriteFileAtomic(
                filePath,
                _ => throw new IOException("simulated power loss"),
                TestContext.Current.CancellationToken);

            // Assert
            await act.Should().ThrowAsync<IOException>();
            (await _fileSystem.File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken)).Should().Be("previous");
        }

        [Theory(Skip = SkipReason, SkipUnless = nameof(IsLinux))]
        [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite)]
        [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite)]
        [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.OtherRead)]
        public async Task Should_preserve_unix_permissions_of_existing_file(UnixFileMode expectedFileMode)
        {
            // Arrange
            var filePath = Path.Combine(_rootPath, "content.json");
            await _fileSystem.File.WriteAllTextAsync(filePath, "previous", TestContext.Current.CancellationToken);
            _fileSystem.File.SetUnixFileMode(filePath, expectedFileMode);

            // Act
            await _fileSystem.WriteFileAtomic(filePath, WriteText("new"), TestContext.Current.CancellationToken);

            // Assert
            _fileSystem.File.GetUnixFileMode(filePath).Should().Be(expectedFileMode);
            (await _fileSystem.File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken)).Should().Be("new");
        }

        [Fact(Skip = SkipReason, SkipUnless = nameof(IsLinux))]
        public async Task Should_not_drift_permissions_across_repeated_writes()
        {
            // Arrange
            var filePath = Path.Combine(_rootPath, "secret.json");
            var expectedFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await _fileSystem.File.WriteAllTextAsync(filePath, "previous", TestContext.Current.CancellationToken);
            _fileSystem.File.SetUnixFileMode(filePath, expectedFileMode);

            // Act
            await _fileSystem.WriteFileAtomic(filePath, WriteText("first"), TestContext.Current.CancellationToken);
            await _fileSystem.WriteFileAtomic(filePath, WriteText("second"), TestContext.Current.CancellationToken);

            // Assert
            _fileSystem.File.GetUnixFileMode(filePath).Should().Be(expectedFileMode);
            (await _fileSystem.File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken)).Should().Be("second");
        }

        [Fact(Skip = SkipReason, SkipUnless = nameof(IsLinux))]
        public async Task Should_keep_unix_permissions_of_previous_file_if_write_fails()
        {
            // Arrange
            var filePath = Path.Combine(_rootPath, "content.json");
            var expectedFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await _fileSystem.File.WriteAllTextAsync(filePath, "previous", TestContext.Current.CancellationToken);
            _fileSystem.File.SetUnixFileMode(filePath, expectedFileMode);

            // Act
            var act = () => _fileSystem.WriteFileAtomic(
                filePath,
                _ => throw new IOException("simulated power loss"),
                TestContext.Current.CancellationToken);

            // Assert
            await act.Should().ThrowAsync<IOException>();
            _fileSystem.File.GetUnixFileMode(filePath).Should().Be(expectedFileMode);
        }

        private static Func<Stream, Task> WriteText(string content)
            => async stream =>
            {
                await using var writer = new StreamWriter(stream, leaveOpen: true);
                await writer.WriteAsync(content);
            };
    }
}
