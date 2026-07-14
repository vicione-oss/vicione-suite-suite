using System.IO.Abstractions.TestingHelpers;
using AwesomeAssertions;
using Core.Module.Extensions;
using Core.Module.Options;
using Core.Tests.Tools;
using Sdk.Testing.Backend;
using TestModule.Backend;
using TestModule.Client;
using TestUiHost;
using Xunit;

namespace Core.Module.Tests.Extensions;

public class FileSystemExtensionsTests
{
    public sealed class GetBackendModuleAssemblyPaths : FileSystemExtensionsTests
    {
        [Fact]
        public void Should_return_existing_dlls_from_path()
        {
            // Arrange
            var config = new TestConfig()
                .ConfigureModuleLoader()
                .BuildConfiguration();

            var loaderOptions = config.GetModuleLoaderTestOptions();
            var fileSystem = new MockFileSystem()
                .SetupTestBackendModule(loaderOptions.ModulesPath!)
                .SetupTestClientModule(loaderOptions.ModulesPath!);

            // Act
            var dllPaths = fileSystem.GetBackendModuleAssemblyPaths(loaderOptions.ModulesPath);

            // Assert
            var expected = TestBackendModule.GetAssemblyDll();
            dllPaths.Should().ContainSingle(k => k.EndsWith(expected, StringComparison.Ordinal));
        }
    }

    public sealed class GetUiModuleAssemblyPaths : FileSystemExtensionsTests
    {
        [Fact]
        public void Should_return_existing_dlls()
        {
            // Arrange
            var config = new TestConfig()
                .ConfigureModuleLoader()
                .BuildConfiguration();

            var loaderOptions = config.GetModuleLoaderTestOptions();
            var fileSystem = new MockFileSystem()
                .SetupTestBackendModule(loaderOptions.ModulesPath!)
                .SetupTestClientModule(loaderOptions.ModulesPath!);

            // Act
            var clientPaths = fileSystem.GetUiModuleAssemblyPaths(loaderOptions.ModulesPath, string.Empty);

            // Assert
            var expected = TestClientModule.GetAssemblyName();
            clientPaths.Should().ContainSingle(k => k.EndsWith(expected, StringComparison.Ordinal));
        }

        [Fact]
        public void Should_throw_if_source_is_not_configured()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var loaderOptions = new ModuleLoaderOptions();

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => fileSystem.GetUiModuleAssemblyPaths(loaderOptions.ModulesPath, string.Empty));
        }

        [Fact]
        public void Should_throw_if_source_is_not_valid_directory()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var config = new TestConfig()
                .ConfigureModuleLoader("path/to/nowhere")
                .BuildConfiguration();

            var loaderOptions = config.GetModuleLoaderTestOptions();

            // Act + Assert

#if !DEBUG // Would never throw in DEBUG due to condition in IFileSystemExtensions.GetModuleDlls
            Assert.Throws<DirectoryNotFoundException>(() => fileSystem.GetUiModuleAssemblyPaths(loaderOptions.ModulesPath, string.Empty));
#endif
        }
    }

    public sealed class GetUiHostModuleAssemblyPaths : FileSystemExtensionsTests
    {
        [Fact]
        public void Should_return_existing_ui_host_dlls()
        {
            // Arrange
            var expected = TestUiHostBackend.GetAssemblyName();
            var config = new TestConfig()
                .ConfigureModuleLoader()
                .BuildConfiguration();

            var loaderOptions = config.GetModuleLoaderTestOptions();
            var fileSystem = new MockFileSystem()
                .SetupTestUiHost(loaderOptions.ModulesPath!)
                .SetupTestClientModule(loaderOptions.ModulesPath!);

            // Act
            var dllPaths = fileSystem.GetUiHostModuleAssemblyPaths(loaderOptions.ModulesPath, fileSystem.Path.GetFileNameWithoutExtension(expected));

            // Assert
            dllPaths.Should().ContainSingle(k => k.EndsWith(expected, StringComparison.Ordinal));
        }
    }

    public sealed class GetDebugModuleVersions
    {
        private const string ModuleId = "ViciOne.ModuleX";
        private const string ModuleRepo = "/module-repo";
        private const string ModulePath = ModuleRepo + "/src/module1/bin/Debug/net9.0";
        private const string ModulesPath = "/path/to/modules";
        private const string ReadmeMd = "readme.md";

        private readonly MockFileSystem _fileSystem = new();

        [Fact]
        public async Task Should_return_versions_for_valid_debug_modules()
        {
            // Arrange
            var moduleDll = ModuleId + ".Backend.dll";
            var moduleDepsJson = ModuleId + ".Backend.deps.json";
            var moduleVersion = "1.51.4";

            _fileSystem.AddEmptyFile(_fileSystem.Path.Combine(ModuleRepo, ReadmeMd));
            _fileSystem.AddEmptyFile(_fileSystem.Path.Combine(ModulePath, moduleDll));
            _fileSystem.AddEmptyFile(_fileSystem.Path.Combine(ModulePath, moduleDepsJson));
            _fileSystem.AddFile(_fileSystem.Path.Combine(ModuleRepo, "VERSION"), new MockFileData($"{moduleVersion}\r\n"));

            var loaderOptions = new ModuleLoaderOptions()
            {
                ModuleDebugPaths = [_fileSystem.Path.Combine(ModuleRepo, "src")],
                ModulesPath = ModulesPath
            };

            // Act
            var result = await _fileSystem.GetDebugModuleVersions(loaderOptions, TestContext.Current.CancellationToken);

            // Assert
            result.Should().ContainKey(ModuleId);
            result[ModuleId].Should().Be(moduleVersion);
        }

        [Fact]
        public async Task Should_skip_when_no_version_file_found()
        {
            // Arrange
            var moduleDll = ModuleId + ".Backend.dll";
            var moduleDepsJson = ModuleId + ".Backend.deps.json";

            _fileSystem.AddEmptyFile(_fileSystem.Path.Combine(ModuleRepo, ReadmeMd));
            _fileSystem.AddEmptyFile(_fileSystem.Path.Combine(ModulePath, moduleDll));
            _fileSystem.AddEmptyFile(_fileSystem.Path.Combine(ModulePath, moduleDepsJson));

            var loaderOptions = new ModuleLoaderOptions()
            {
                ModuleDebugPaths = [_fileSystem.Path.Combine(ModuleRepo, "src")],
                ModulesPath = ModulesPath
            };

            // Act
            var result = await _fileSystem.GetDebugModuleVersions(loaderOptions, TestContext.Current.CancellationToken);

            // Assert
            result.Should().NotContainKey(ModuleId);
        }

        [Fact]
        public async Task Should_skip_when_module_id_is_invalid()
        {
            // Arrange
            var moduleDll = ModuleId + ".Unknown.dll";
            var moduleDepsJson = ModuleId + ".Unknown.deps.json";

            _fileSystem.AddEmptyFile(_fileSystem.Path.Combine(ModulePath, moduleDll));
            _fileSystem.AddEmptyFile(_fileSystem.Path.Combine(ModulePath, moduleDepsJson));

            var loaderOptions = new ModuleLoaderOptions()
            {
                ModuleDebugPaths = [_fileSystem.Path.Combine(ModuleRepo, "src")],
                ModulesPath = ModulesPath
            };

            // Act
            var result = await _fileSystem.GetDebugModuleVersions(loaderOptions, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeEmpty();
        }

        [Theory]
        [InlineData("1.42.5  \r\n", "1.42.5")]
        [InlineData("\t1.42.5 \n", "1.42.5")]
        [InlineData("\r1.42.5\t\r\n", "1.42.5")]
        [InlineData("\f   1.42.5 \n\n\r", "1.42.5")]
        public async Task Should_remove_special_chars_from_version(string versionFileContent, string expected)
        {
            // Arrange
            var moduleDll = ModuleId + ".Backend.dll";
            var moduleDepsJson = ModuleId + ".Backend.deps.json";

            _fileSystem.AddEmptyFile(_fileSystem.Path.Combine(ModuleRepo, ReadmeMd));
            _fileSystem.AddEmptyFile(_fileSystem.Path.Combine(ModulePath, moduleDll));
            _fileSystem.AddEmptyFile(_fileSystem.Path.Combine(ModulePath, moduleDepsJson));
            _fileSystem.AddFile(_fileSystem.Path.Combine(ModuleRepo, "VERSION"), new MockFileData(versionFileContent));

            var loaderOptions = new ModuleLoaderOptions()
            {
                ModuleDebugPaths = [_fileSystem.Path.Combine(ModuleRepo, "src")],
                ModulesPath = ModulesPath
            };

            // Act
            var result = await _fileSystem.GetDebugModuleVersions(loaderOptions, TestContext.Current.CancellationToken);

            // Assert
            result.Should().ContainKey(ModuleId);
            result[ModuleId].Should().Be(expected);
        }
    }
}
