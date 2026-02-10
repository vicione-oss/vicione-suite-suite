using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.Modules.Services;
using Core.Tests.Tools;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sdk.Testing.Backend;
using TestModule.Backend;
using Xunit;

namespace Core.OS.Tests.Modules.Services;

public class WorkspaceManagementTests
{
    private readonly MockFileSystem _fileSystem = new();
    private readonly IConfiguration _config = new TestConfig()
        .AddTestUiHost()
        .AddTestBackendClientModule()
        .BuildConfiguration();

    private ServiceProvider CreateServiceProvider(IFileSystem? fileSystem = null)
        => new ServiceCollection()
            .AddSingleton<WorkspaceManagement>()
            .AddSingleton(fileSystem ?? _fileSystem)
            .AddSingleton(Substitute.For<ILogger<WorkspaceManagement>>())
            .AddSingleton<IOptions<InstanceOptions>>(_ => Options.Create(_config.GetInstanceOptions()))
            .BuildServiceProvider();

    public sealed class GetHomeDirectory : WorkspaceManagementTests
    {
        [Fact]
        public async Task Should_create_directory_from_settings()
        {
            // Arrange
            var instanceOptions = _config.GetInstanceOptions();
            await using var serviceProvider = CreateServiceProvider();
            var workspaceMgmt = serviceProvider.GetRequiredService<WorkspaceManagement>();

            // Act
            var moduleHome = workspaceMgmt.GetHomeDirectory(TestBackendModule.Id);

            // Assert
            Assert.Equal(_fileSystem.Path.Combine(_fileSystem.GetRootedHomeDirectory(instanceOptions), TestBackendModule.Id), moduleHome);
        }

        [Fact]
        public void Should_throw_on_unauthorized_access()
        {
            // Arrange
            var mockFileSystem = Substitute.For<IFileSystem>();
            var mockDirectory = Substitute.For<IDirectory>();
            var mockFileData = Substitute.For<IMockFileDataAccessor>();
            mockDirectory.GetCurrentDirectory()
                .Returns(Directory.GetCurrentDirectory());

            mockDirectory.CreateDirectory(Arg.Any<string>()).Throws(new UnauthorizedAccessException());
            mockFileSystem.Directory.Returns(mockDirectory);
            var mockPath = new MockPath(mockFileData);
            mockFileSystem.Path.Returns(mockPath);

            // Act + Assert
            Assert.Throws<UnauthorizedAccessException>(() => CreateServiceProvider(mockFileSystem)
                .GetRequiredService<WorkspaceManagement>()
                .GetHomeDirectory(TestBackendModule.Id));
        }
    }

    public sealed class GetCacheDirectory : WorkspaceManagementTests
    {
        [Fact]
        public async Task Should_create_directory_from_settings()
        {
            // Arrange
            var instanceOptions = _config.GetInstanceOptions();
            await using var serviceProvider = CreateServiceProvider();
            var workspaceMgmt = serviceProvider.GetRequiredService<WorkspaceManagement>();

            // Act
            var moduleCache = workspaceMgmt.GetCacheDirectory(TestBackendModule.Id);

            // Assert
            Assert.Equal(_fileSystem.Path.Combine(_fileSystem.GetRootedCacheDirectory(instanceOptions), TestBackendModule.Id), moduleCache);
        }

        [Fact]
        public void Should_throw_on_unauthorized_access()
        {
            // Arrange
            var mockFileSystem = Substitute.For<IFileSystem>();
            var mockDirectory = Substitute.For<IDirectory>();
            var mockFileData = Substitute.For<IMockFileDataAccessor>();
            mockDirectory.GetCurrentDirectory()
                .Returns(Directory.GetCurrentDirectory());

            mockDirectory.CreateDirectory(Arg.Any<string>()).Throws(new UnauthorizedAccessException());
            mockFileSystem.Directory.Returns(mockDirectory);
            var mockPath = new MockPath(mockFileData);
            mockFileSystem.Path.Returns(mockPath);

            // Act + Assert
            Assert.Throws<UnauthorizedAccessException>(() => CreateServiceProvider(mockFileSystem)
                .GetRequiredService<WorkspaceManagement>()
                .GetCacheDirectory(TestBackendModule.Id));
        }
    }

    public sealed class WriteResetHomeDirectoryFlag : WorkspaceManagementTests
    {
        [Fact]
        public async Task Should_create_reset_trigger_file()
        {
            // Arrange
            var instanceOptions = _config.GetInstanceOptions();
            await using var serviceProvider = CreateServiceProvider();
            var workspaceMgmt = serviceProvider.GetRequiredService<WorkspaceManagement>();

            // Act
            workspaceMgmt.WriteResetHomeDirectoryFlag(TestBackendModule.Id);

            // Assert
            var expected = _fileSystem.Path.Combine(
                _fileSystem.GetRootedHomeDirectory(instanceOptions),
                TestBackendModule.Id,
                WorkspaceManagement.ResetDirectoryIdentifier);

            _fileSystem.File.Exists(expected).Should().BeTrue();
        }
    }

    public sealed class WriteResetCacheDirectoryFlag : WorkspaceManagementTests
    {
        [Fact]
        public async Task Should_create_reset_trigger_file()
        {
            // Arrange
            var instanceOptions = _config.GetInstanceOptions();
            await using var serviceProvider = CreateServiceProvider();
            var workspaceMgmt = serviceProvider.GetRequiredService<WorkspaceManagement>();

            // Act
            workspaceMgmt.WriteResetCacheDirectoryFlag(TestBackendModule.Id);

            // Assert
            var expected = _fileSystem.Path.Combine(
                _fileSystem.GetRootedCacheDirectory(instanceOptions),
                TestBackendModule.Id,
                WorkspaceManagement.ResetDirectoryIdentifier);

            _fileSystem.File.Exists(expected).Should().BeTrue();
        }
    }

    public sealed class ResetMarkedModuleDirectories : WorkspaceManagementTests
    {
        [Fact]
        public void Should_remove_marked_module_home_directory()
        {
            // Arrange
            var instanceOptions = _config.GetInstanceOptions();
            var moduleHome = _fileSystem.Path.Combine(_fileSystem.GetRootedHomeDirectory(instanceOptions), TestBackendModule.Id);
            var triggerFile = _fileSystem.Path.Combine(moduleHome, WorkspaceManagement.ResetDirectoryIdentifier);

            _fileSystem.Directory.CreateDirectory(moduleHome);
            _fileSystem.File.Create(triggerFile);

            // Act
            WorkspaceManagement.ResetMarkedModuleWorkspace(_fileSystem, instanceOptions, Substitute.For<Serilog.ILogger>());

            // Assert
            _fileSystem.Directory.Exists(moduleHome).Should().BeFalse();
        }

        [Fact]
        public void Should_remove_marked_module_cache_directory()
        {
            // Arrange
            var instanceOptions = _config.GetInstanceOptions();
            var moduleCache = _fileSystem.Path.Combine(_fileSystem.GetRootedCacheDirectory(instanceOptions), TestBackendModule.Id);
            var triggerFile = _fileSystem.Path.Combine(moduleCache, WorkspaceManagement.ResetDirectoryIdentifier);

            _fileSystem.Directory.CreateDirectory(moduleCache);
            _fileSystem.File.Create(triggerFile);

            // Act
            WorkspaceManagement.ResetMarkedModuleWorkspace(_fileSystem, instanceOptions, Substitute.For<Serilog.ILogger>());

            // Assert
            _fileSystem.Directory.Exists(moduleCache).Should().BeFalse();
        }

        [Fact]
        public void Should_not_touch_module_home_if_not_marked()
        {
            // Arrange
            var instanceOptions = _config.GetInstanceOptions();
            var otherModuleId = "Module.Not.Marked";
            var moduleHome = _fileSystem.Path.Combine(_fileSystem.GetRootedHomeDirectory(instanceOptions), TestBackendModule.Id);
            var otherHome = _fileSystem.Path.Combine(_fileSystem.GetRootedHomeDirectory(instanceOptions), otherModuleId);
            var triggerFile = _fileSystem.Path.Combine(moduleHome, WorkspaceManagement.ResetDirectoryIdentifier);

            _fileSystem.Directory.CreateDirectory(moduleHome);
            _fileSystem.Directory.CreateDirectory(otherHome);
            _fileSystem.File.Create(triggerFile);

            // Act
            WorkspaceManagement.ResetMarkedModuleWorkspace(_fileSystem, instanceOptions, Substitute.For<Serilog.ILogger>());

            // Assert
            _fileSystem.Directory.Exists(moduleHome).Should().BeFalse();
            _fileSystem.Directory.Exists(otherHome).Should().BeTrue();
        }

        [Fact]
        public void Should_not_touch_module_cache_if_not_marked()
        {
            // Arrange
            var instanceOptions = _config.GetInstanceOptions();
            var otherModuleId = "Module.Not.Marked";
            var moduleCache = _fileSystem.Path.Combine(_fileSystem.GetRootedCacheDirectory(instanceOptions), TestBackendModule.Id);
            var otherCache = _fileSystem.Path.Combine(_fileSystem.GetRootedCacheDirectory(instanceOptions), otherModuleId);
            var triggerFile = _fileSystem.Path.Combine(moduleCache, WorkspaceManagement.ResetDirectoryIdentifier);

            _fileSystem.Directory.CreateDirectory(moduleCache);
            _fileSystem.Directory.CreateDirectory(otherCache);
            _fileSystem.File.Create(triggerFile);

            // Act
            WorkspaceManagement.ResetMarkedModuleWorkspace(_fileSystem, instanceOptions, Substitute.For<Serilog.ILogger>());

            // Assert
            _fileSystem.Directory.Exists(moduleCache).Should().BeFalse();
            _fileSystem.Directory.Exists(otherCache).Should().BeTrue();
        }
    }


}
