using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using System.Text.Json;
using Core.Module.Utils;
using Core.OS.Instance;
using Core.OS.Instance.Services;
using Core.OS.Modules;
using Core.OS.Modules.Extensions;
using AwesomeAssertions;
using NSubstitute;
using Sdk.Messaging;
using Sdk.Modules;
using Xunit;

namespace Core.OS.Tests.Modules.Extensions;

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
            await fileSystem.EnsureModuleVersionsFile(_instanceOptions, null, _logger);

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
}
