using System.Globalization;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text;
using Blazor.Server.Backend;
using Blazor.Server.Backend.Contracts;
using Blazor.Server.Backend.Services;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;
using Sdk.Client.Contracts;
using Sdk.Client.Models;
using UploadLocalization = Blazor.Server.Backend.Services.Localization.StreamUploadHandler;

namespace Blazor.Server.Tests.Services;

public class StreamUploadHandlerTests
{
    public sealed class Execute
    {
        private readonly ILogger<StreamUploadHandler<BlazorServerBackendModule, DeviceImageContext>> _logger = Substitute.For<ILogger<StreamUploadHandler<BlazorServerBackendModule, DeviceImageContext>>>();
        private readonly StreamUploadHandlerOptions _options = new();
        private readonly IWorkspaceProvider<BlazorServerBackendModule> _workspaceProvider = Substitute.For<IWorkspaceProvider<BlazorServerBackendModule>>();

        private string SetupSystemWorkspace(MockFileSystem fileSystem, bool createWorkspaceDirectory = true)
        {
            var drive = fileSystem.AllDrives.First();
            var info = fileSystem.GetDrive(drive);
            info.AvailableFreeSpace = 1024 * 1024;

            var cacheFolder = fileSystem.Path.Combine(drive, "cache");
            var systemWorkspace = fileSystem.Path.Combine(cacheFolder, "system");
            fileSystem.AddDirectory(cacheFolder);

            if (createWorkspaceDirectory)
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
            fileSystem.Path.Returns(new MockFileSystem().Path);
            fileSystem.DriveInfo.New("cache").AvailableFreeSpace.Returns(1);

            var sut = new StreamUploadHandler<BlazorServerBackendModule, DeviceImageContext>(_workspaceProvider, fileSystem, _options, _logger);

            var stream = new MemoryStream(Encoding.UTF8.GetBytes("hello"));

            // Act
            var result = await sut.Execute(stream, "file.txt", TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<StreamUploadErrorResult>()
                .Which.Message.Should().Be(UploadLocalization.InsufficientDiskSpace);
        }

        [Fact]
        public async Task Should_upload_and_return_success_result()
        {
            // Arrange                                    
            var fileSystem = new MockFileSystem();
            var systemWorkspace = SetupSystemWorkspace(fileSystem);

            var sut = new StreamUploadHandler<BlazorServerBackendModule, DeviceImageContext>(_workspaceProvider, fileSystem, _options, _logger);

            var content = Encoding.UTF8.GetBytes("HelloWorld");
            var inputStream = new MemoryStream(content);

            // Act
            var result = await sut.Execute(inputStream, "file.txt", TestContext.Current.CancellationToken);

            // Assert
            ResultDestinationFileShouldExist(result, fileSystem.Path.Combine(systemWorkspace, "file.txt"));
        }

        [Fact]
        public async Task Should_create_missing_upload_directory_via_file_system_abstraction()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var systemWorkspace = SetupSystemWorkspace(fileSystem, createWorkspaceDirectory: false);

            var sut = new StreamUploadHandler<BlazorServerBackendModule, DeviceImageContext>(_workspaceProvider, fileSystem, _options, _logger);

            var inputStream = new MemoryStream("HelloWorld"u8.ToArray());

            // Act
            var result = await sut.Execute(inputStream, "file.txt", TestContext.Current.CancellationToken);

            // Assert
            fileSystem.Directory.Exists(systemWorkspace).Should().BeTrue();
            ResultDestinationFileShouldExist(result, fileSystem.Path.Combine(systemWorkspace, "file.txt"));
        }

        [Fact]
        public async Task Should_use_options_path_transformation()
        {
            // Arrange                                    
            var fileSystem = new MockFileSystem();
            var systemWorkspace = SetupSystemWorkspace(fileSystem);
            var parent = fileSystem.Directory.GetParent(systemWorkspace);
            var transform = parent!.CreateSubdirectory("transform");

            _options.PathTransform = _ => transform.FullName;

            var sut = new StreamUploadHandler<BlazorServerBackendModule, DeviceImageContext>(_workspaceProvider, fileSystem, _options, _logger);

            var content = "HelloWorld"u8.ToArray();
            var inputStream = new MemoryStream(content);

            // Act
            var result = await sut.Execute(inputStream, "file.txt", TestContext.Current.CancellationToken);

            // Assert
            var expectedFilePath = fileSystem.Path.Combine(transform.FullName, "file.txt");
            var expectedFileContents = await fileSystem.File.ReadAllBytesAsync(expectedFilePath, TestContext.Current.CancellationToken);
            ResultDestinationFileShouldExist(result, expectedFilePath);
            content.Should().BeEquivalentTo(expectedFileContents);
        }

        [Fact]
        public async Task Should_invoke_onprogress_callback()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            _ = SetupSystemWorkspace(fileSystem);

            var sut = new StreamUploadHandler<BlazorServerBackendModule, DeviceImageContext>(_workspaceProvider, fileSystem, _options, _logger);

            IStreamUploadProgress? captured = null;
            sut.OnProgress = p => { captured = p; return Task.CompletedTask; };

            var input = new MemoryStream("data"u8.ToArray());

            // Act
            await sut.Execute(input, "file.txt", TestContext.Current.CancellationToken);

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
            fileSystem.Path.Returns(new MockFileSystem().Path);
            fileSystem.File.Create(Arg.Any<string>()).Returns(_ => throw new IOException("disk error"));

            var sut = new StreamUploadHandler<BlazorServerBackendModule, DeviceImageContext>(_workspaceProvider, fileSystem, _options, _logger);
            var input = new MemoryStream("foo"u8.ToArray());

            // Act
            var result = await sut.Execute(input, "bad.txt", TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<StreamUploadErrorResult>();
        }

        [Fact]
        public async Task Should_return_localized_error_result_when_path_transformation_fails()
        {
            // Arrange
            _options.PathTransform = _ => throw new InvalidOperationException();

            var sut = new StreamUploadHandler<BlazorServerBackendModule, DeviceImageContext>(_workspaceProvider, new MockFileSystem(), _options, _logger);

            // Act
            var result = await sut.Execute(new MemoryStream(), "file.txt", TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<StreamUploadErrorResult>()
                .Which.Message.Should().Be(string.Format(CultureInfo.CurrentCulture, UploadLocalization.PathTransformationFailed, "file.txt"));
        }

        [Fact]
        public async Task Should_return_localized_error_result_when_filename_transformation_fails()
        {
            // Arrange
            _options.FilenameTransform = _ => throw new InvalidOperationException();

            var sut = new StreamUploadHandler<BlazorServerBackendModule, DeviceImageContext>(_workspaceProvider, new MockFileSystem(), _options, _logger);

            // Act
            var result = await sut.Execute(new MemoryStream(), "file.txt", TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<StreamUploadErrorResult>()
                .Which.Message.Should().Be(string.Format(CultureInfo.CurrentCulture, UploadLocalization.FilenameTransformationFailed, "file.txt"));
        }

        [Theory]
        [InlineData("")]
        [InlineData(".")]
        [InlineData("..")]
        public async Task Should_reject_file_name_without_file_segment(string fileName)
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            _ = SetupSystemWorkspace(fileSystem);

            var sut = new StreamUploadHandler<BlazorServerBackendModule, DeviceImageContext>(_workspaceProvider, fileSystem, _options, _logger);

            // Act
            var result = await sut.Execute(new MemoryStream("data"u8.ToArray()), fileName, TestContext.Current.CancellationToken);

            // Assert
            InvalidFileNameShouldBeRejected(result, fileSystem, fileName);
        }

        [Fact]
        public async Task Should_reject_file_name_with_parent_directory_segments()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            _ = SetupSystemWorkspace(fileSystem);
            var fileName = fileSystem.Path.Combine("..", "..", "appsettings.json");

            var sut = new StreamUploadHandler<BlazorServerBackendModule, DeviceImageContext>(_workspaceProvider, fileSystem, _options, _logger);

            // Act
            var result = await sut.Execute(new MemoryStream("data"u8.ToArray()), fileName, TestContext.Current.CancellationToken);

            // Assert
            InvalidFileNameShouldBeRejected(result, fileSystem, fileName);
        }

        [Fact]
        public async Task Should_reject_rooted_file_name()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            _ = SetupSystemWorkspace(fileSystem);
            var fileName = fileSystem.Path.Combine(fileSystem.AllDrives.First(), "etc", "passwd");

            var sut = new StreamUploadHandler<BlazorServerBackendModule, DeviceImageContext>(_workspaceProvider, fileSystem, _options, _logger);

            // Act
            var result = await sut.Execute(new MemoryStream("data"u8.ToArray()), fileName, TestContext.Current.CancellationToken);

            // Assert
            InvalidFileNameShouldBeRejected(result, fileSystem, fileName);
        }

        [Fact]
        public async Task Should_reject_filename_transformation_that_returns_a_subdirectory()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            _ = SetupSystemWorkspace(fileSystem);
            _options.FilenameTransform = name => fileSystem.Path.Combine("sub", name);

            var sut = new StreamUploadHandler<BlazorServerBackendModule, DeviceImageContext>(_workspaceProvider, fileSystem, _options, _logger);

            // Act
            var result = await sut.Execute(new MemoryStream("data"u8.ToArray()), "file.txt", TestContext.Current.CancellationToken);

            // Assert
            InvalidFileNameShouldBeRejected(result, fileSystem, "file.txt");
        }

        private static void InvalidFileNameShouldBeRejected(IStreamUploadResult result, MockFileSystem fileSystem, string fileName)
        {
            result.Should().BeOfType<StreamUploadErrorResult>()
                .Which.Message.Should().Be(string.Format(CultureInfo.CurrentCulture, UploadLocalization.InvalidFileName, fileName));
            fileSystem.AllFiles.Should().BeEmpty();
        }

        private static void ResultDestinationFileShouldExist(IStreamUploadResult result, string expectedFilePath)
        {
            result.Should().BeOfType<StreamUploadSuccessResult>();

            var uploadResult = Assert.IsType<StreamUploadSuccessResult>(result);

            uploadResult.DestinationFile.Should().Be(expectedFilePath);
        }
    }
}
