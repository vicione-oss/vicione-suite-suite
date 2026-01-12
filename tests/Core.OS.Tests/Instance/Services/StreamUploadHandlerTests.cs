using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text;
using AwesomeAssertions;
using Core.OS.Instance;
using Core.OS.Instance.Contracts;
using Core.OS.Instance.Services;
using Core.OS.Modules;
using Core.Shared.Instance.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Backend.Modules;
using Xunit;

namespace Core.OS.Tests.Instance.Services;

public class StreamUploadHandlerTests
{
    public sealed class Execute
    {
        private readonly ILogger<StreamUploadHandler<SystemBackendModule, DeviceImageContext>> _logger = Substitute.For<ILogger<StreamUploadHandler<SystemBackendModule, DeviceImageContext>>>();
        private readonly StreamUploadHandlerOptions<DeviceImageContext> _options = new();
        private readonly IWorkspaceProvider<SystemBackendModule> _workspaceProvider = Substitute.For<IWorkspaceProvider<SystemBackendModule>>();

        private string SetupSystemWorkspace(MockFileSystem fileSystem)
        {
            var drive = fileSystem.AllDrives.First();
            var info = fileSystem.GetDrive(drive);
            info.AvailableFreeSpace = 1024 * 1024;

            var cacheFolder = fileSystem.Path.Combine(drive, "cache");
            var systemWorkspace = fileSystem.Path.Combine(cacheFolder, "system");
            fileSystem.AddDirectory(cacheFolder);
            fileSystem.AddDirectory(systemWorkspace);

            _workspaceProvider.Cache.Returns(systemWorkspace);

            return systemWorkspace;
        }

        [Fact]
        public async Task Should_return_error_when_not_enough_free_space()
        {
            // Arrange
            _workspaceProvider.Cache.Returns("cache");

            var fileSystem = Substitute.For<IFileSystem>();
            fileSystem.DriveInfo.New("cache").AvailableFreeSpace.Returns(1);

            var sut = new StreamUploadHandler<SystemBackendModule, DeviceImageContext>(_workspaceProvider, fileSystem, Options.Create(_options), _logger);

            var stream = new MemoryStream(Encoding.UTF8.GetBytes("hello"));

            // Act
            var result = await sut.Execute(stream, "file.txt", CancellationToken.None);

            // Assert
            result.Should().BeOfType<StreamUploadErrorResult>();
        }

        [Fact]
        public async Task Should_upload_and_return_success_result()
        {
            // Arrange                                    
            var fileSystem = new MockFileSystem();
            var systemWorkspace = SetupSystemWorkspace(fileSystem);

            var sut = new StreamUploadHandler<SystemBackendModule, DeviceImageContext>(_workspaceProvider, fileSystem, Options.Create(_options), _logger);

            var content = Encoding.UTF8.GetBytes("HelloWorld");
            var inputStream = new MemoryStream(content);

            // Act
            var result = await sut.Execute(inputStream, "file.txt", CancellationToken.None);

            // Assert
            ResultDestinationFileShouldExist(result, fileSystem.Path.Combine(systemWorkspace, "file.txt"));
        }

        [Fact]
        public async Task Should_use_options_path_transformation()
        {
            // Arrange                                    
            var fileSystem = new MockFileSystem();
            var systemWorkspace = SetupSystemWorkspace(fileSystem);
            var parent = fileSystem.Directory.GetParent(systemWorkspace);
            var transform = parent!.CreateSubdirectory("tranform");

            _options.PathTransform = (oldPath) =>
            {
                return transform.FullName;
            };

            var sut = new StreamUploadHandler<SystemBackendModule, DeviceImageContext>(_workspaceProvider, fileSystem, Options.Create(_options), _logger);

            var content = Encoding.UTF8.GetBytes("HelloWorld");
            var inputStream = new MemoryStream(content);

            // Act
            var result = await sut.Execute(inputStream, "file.txt", CancellationToken.None);

            // Assert
            var expectedFilePath = fileSystem.Path.Combine(transform.FullName, "file.txt");
            var expectedFileContents = await fileSystem.File.ReadAllBytesAsync(expectedFilePath);
            ResultDestinationFileShouldExist(result, expectedFilePath);
            content.Should().BeEquivalentTo(expectedFileContents);
        }

        [Fact]
        public async Task Should_invoke_onprogress_callback()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var systemWorkspace = SetupSystemWorkspace(fileSystem);

            var sut = new StreamUploadHandler<SystemBackendModule, DeviceImageContext>(_workspaceProvider, fileSystem, Options.Create(_options), _logger);

            IStreamUploadProgress? captured = null;
            sut.OnProgress = p => { captured = p; return Task.CompletedTask; };

            var input = new MemoryStream(Encoding.UTF8.GetBytes("data"));

            // Act
            await sut.Execute(input, "file.txt", CancellationToken.None);

            // Assert
            captured.Should().NotBeNull();
            captured!.Filename.Should().Be("file.txt");
            captured.BytesUploaded.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task Should_return_error_result_when_exception_occurs()
        {
            // Arrange
            _workspaceProvider.Cache.Returns("cache");

            var fileSystem = Substitute.For<IFileSystem>();
            fileSystem.DriveInfo.New("cache").AvailableFreeSpace.Returns(1024 * 1024);
            fileSystem.File.Create(Arg.Any<string>()).Returns(ci => throw new IOException("disk error"));
            fileSystem.Path.Combine("cache", "bad.txt").Returns("cache/bad.txt");

            var sut = new StreamUploadHandler<SystemBackendModule, DeviceImageContext>(_workspaceProvider, fileSystem, Options.Create(_options), _logger);
            var input = new MemoryStream(Encoding.UTF8.GetBytes("foo"));

            // Act
            var result = await sut.Execute(input, "bad.txt", CancellationToken.None);

            // Assert
            result.Should().BeOfType<StreamUploadErrorResult>();
        }

        private static void ResultDestinationFileShouldExist(IStreamUploadResult result, string expectedFilePath)
        {
            result.Should().BeOfType<StreamUploadSuccessResult>();

            var uploadResult = Assert.IsType<StreamUploadSuccessResult>(result);

            uploadResult.DestinationFile.Should().Be(expectedFilePath);
        }
    }
}
