using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using AwesomeAssertions;
using Core.OS.Instance.Extensions;
using Core.OS.Modules.Services;
using Core.Tests.Tools;
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
            .AddSingleton(_ => Options.Create(_config.GetInstanceOptions()))
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
            moduleHome.Should().Be(_fileSystem.Path.Combine(_fileSystem.GetRootedHomeDirectory(instanceOptions), TestBackendModule.Id));
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
            moduleCache.Should().Be(_fileSystem.Path.Combine(_fileSystem.GetRootedCacheDirectory(instanceOptions), TestBackendModule.Id));
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
}
