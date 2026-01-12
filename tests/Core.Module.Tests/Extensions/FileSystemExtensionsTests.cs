using System.IO.Abstractions.TestingHelpers;
using Core.Module.Extensions;
using Core.Module.Options;
using Core.Tests.Tools;
using AwesomeAssertions;
using Sdk.Testing.Backend;
using TestModule.Backend;
using TestModule.Client;
using TestUiHost;
using Xunit;

namespace Core.Module.Tests.Extensions;

public class FileSystemExtensionsTests
{
    public class GetBackendModuleAssemblyPaths : FileSystemExtensionsTests
    {
        [Fact]
        public void Should_return_existing_dlls_from_path()
        {
            // Arrange
            var config = new TestConfig()
                .ConfigureModuleLoader()
                .BuildConfiguration();

            var loaderOptions = config.GetTestModuleLoaderOptions();
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

    public class GetUiModuleAssemblyPaths : FileSystemExtensionsTests
    {
        [Fact]
        public void Should_return_existing_dlls()
        {
            // Arrange
            var config = new TestConfig()
                .ConfigureModuleLoader()
                .BuildConfiguration();

            var loaderOptions = config.GetTestModuleLoaderOptions();
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

            var loaderOptions = config.GetTestModuleLoaderOptions();

            // Act + Assert
            
#if !DEBUG // Would never throw in DEBUG due to condition in IFileSystemExtensions.GetModuleDlls
            Assert.Throws<DirectoryNotFoundException>(() => fileSystem.GetUiModuleAssemblyPaths(loaderOptions.ModulesPath, string.Empty));
#endif
        }
    }

    public class GetUiHostModuleAssemblyPaths : FileSystemExtensionsTests
    {
        [Fact]
        public void Should_return_existing_ui_host_dlls()
        {
            // Arrange
            var expected = TestUiHostBackend.GetAssemblyName();
            var config = new TestConfig()
                .ConfigureModuleLoader()
                .BuildConfiguration();

            var loaderOptions = config.GetTestModuleLoaderOptions();
            var fileSystem = new MockFileSystem()
                .SetupTestUiHost(loaderOptions.ModulesPath!)
                .SetupTestClientModule(loaderOptions.ModulesPath!);

            // Act
            var dllPaths = fileSystem.GetUiHostModuleAssemblyPaths(loaderOptions.ModulesPath, fileSystem.Path.GetFileNameWithoutExtension(expected));

            // Assert
            dllPaths.Should().ContainSingle(k => k.EndsWith(expected, StringComparison.Ordinal));
        }
    }
}
