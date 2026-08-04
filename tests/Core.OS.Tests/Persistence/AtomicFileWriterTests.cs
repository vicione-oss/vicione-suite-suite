using System.IO.Abstractions.TestingHelpers;
using System.Text;
using Core.OS.Persistence;

namespace Core.OS.Tests.Persistence;

public class AtomicFileWriterTests
{
    public sealed class WriteAsync : AtomicFileWriterTests
    {
        private const string FilePath = "/data/output.json";

        private readonly MockFileSystem _fileSystem = new();
        private readonly AtomicFileWriter _writer;

        public WriteAsync() => _writer = new AtomicFileWriter(_fileSystem);

        [Fact]
        public async Task Should_write_content_to_file()
        {
            // Arrange
            const string content = "{ \"key\": \"value\" }";
            _fileSystem.AddDirectory("/data");

            // Act
            await _writer.WriteAsync(FilePath, stream =>
                stream.WriteAsync(Encoding.UTF8.GetBytes(content)).AsTask(), TestContext.Current.CancellationToken);

            // Assert
            (await _fileSystem.File.ReadAllTextAsync(FilePath, TestContext.Current.CancellationToken)).Should().Be(content);
        }

        [Fact]
        public async Task Should_create_directory_if_it_does_not_exist()
        {
            // Arrange
            const string nestedPath = "/data/nested/dir/output.json";

            // Act
            await _writer.WriteAsync(nestedPath, stream =>
                stream.WriteAsync("content"u8.ToArray()).AsTask(), TestContext.Current.CancellationToken);

            // Assert
            _fileSystem.Directory.Exists("/data/nested/dir").Should().BeTrue();
            _fileSystem.File.Exists(nestedPath).Should().BeTrue();
        }

        [Fact]
        public async Task Should_not_leave_temp_file_after_successful_write()
        {
            // Arrange
            _fileSystem.AddDirectory("/data");

            // Act
            await _writer.WriteAsync(FilePath, stream =>
                stream.WriteAsync("content"u8.ToArray()).AsTask(), TestContext.Current.CancellationToken);

            // Assert
            _fileSystem.File.Exists(FilePath + ".tmp").Should().BeFalse();
        }

        [Fact]
        public async Task Should_overwrite_existing_file()
        {
            // Arrange
            const string originalContent = "original";
            const string updatedContent = "updated";
            _fileSystem.AddFile(FilePath, new MockFileData(originalContent));

            // Act
            await _writer.WriteAsync(FilePath, stream =>
                stream.WriteAsync(Encoding.UTF8.GetBytes(updatedContent)).AsTask(), TestContext.Current.CancellationToken);

            // Assert
            (await _fileSystem.File.ReadAllTextAsync(FilePath, TestContext.Current.CancellationToken)).Should().Be(updatedContent);
        }
    }
}
