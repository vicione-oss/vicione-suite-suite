using System.IO.Abstractions;
using AwesomeAssertions;
using Core.Artifacts.Extensions;
using Xunit;

namespace Core.Artifacts.Tests;

public class IFileSystemExtensionsTests
{
    public sealed class EnsureContainedPath : IFileSystemExtensionsTests
    {
        private readonly IFileSystem _fileSystem = new FileSystem();
        private readonly string _root;

        public EnsureContainedPath()
            => _root = _fileSystem.Path.GetFullPath("test-root");

        [Fact]
        public void Returns_resolved_path_for_valid_entry()
        {
            // Act
            var result = _fileSystem.EnsureContainedPath(_root, "file.txt");

            // Assert
            result.Should().Be(_fileSystem.Path.Combine(_root, "file.txt"));
        }

        [Fact]
        public void Allows_nested_subdirectory_entry()
        {
            // Act
            var result = _fileSystem.EnsureContainedPath(_root, "sub/nested/file.txt");

            // Assert
            var expected = _fileSystem.Path.GetFullPath(_fileSystem.Path.Combine(_root, "sub/nested/file.txt"));
            result.Should().Be(expected);
            result.Should().StartWith(_root + _fileSystem.Path.DirectorySeparatorChar);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Throws_for_empty_entry(string entryName)
        {
            // Act
            var act = () => _fileSystem.EnsureContainedPath(_root, entryName);

            // Assert
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*empty name*");
        }

        [Theory]
        [InlineData("../evil.txt")]
        [InlineData("../../evil.txt")]
        [InlineData("sub/../../evil.txt")]
        [InlineData("nested/../../../evil.txt")]
        public void Throws_for_traversing_entry(string entryName)
        {
            // Act
            var act = () => _fileSystem.EnsureContainedPath(_root, entryName);

            // Assert
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*escapes the target directory*");
        }

        [Fact]
        public void Throws_for_rooted_entry()
        {
            // Act
            var act = () => _fileSystem.EnsureContainedPath(_root, "/etc/passwd");

            // Assert
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*rooted path*");
        }
    }
}
