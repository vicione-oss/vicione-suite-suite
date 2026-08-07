using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Core.OS.EnvironmentOverrides;
using NSubstitute.ExceptionExtensions;

namespace Core.OS.Tests.EnvironmentOverrides;

public sealed class EnvironmentOverridesLoaderTests : IDisposable
{
    private const string OverrideFilePath = "/data/env-overrides.env";

    private readonly string _key = "VICIONE_TEST_" + Guid.NewGuid().ToString("N");
    private readonly MockFileSystem _fileSystem = new();

    public void Dispose() => Environment.SetEnvironmentVariable(_key, null);

    [Fact]
    public async Task Should_overwrite_existing_environment_variable()
    {
        // Arrange
        Environment.SetEnvironmentVariable(_key, "inherited-value");
        _fileSystem.AddFile(OverrideFilePath, new MockFileData($"{_key}=\"override-value\"\n"));

        // Act
        await EnvironmentOverridesLoader.Apply(_fileSystem, OverrideFilePath);

        // Assert
        Environment.GetEnvironmentVariable(_key).Should().Be("override-value");
    }

    [Fact]
    public async Task Should_set_variable_that_was_not_present_before()
    {
        // Arrange
        _fileSystem.AddFile(OverrideFilePath, new MockFileData($"{_key}=\"new-value\"\n"));

        // Act
        await EnvironmentOverridesLoader.Apply(_fileSystem, OverrideFilePath);

        // Assert
        Environment.GetEnvironmentVariable(_key).Should().Be("new-value");
    }

    [Fact]
    public async Task Should_leave_inherited_variable_untouched_when_absent_from_file()
    {
        // Arrange — models deleting an entry: on the next load it is no longer overridden.
        Environment.SetEnvironmentVariable(_key, "inherited-value");
        _fileSystem.AddFile(OverrideFilePath, new MockFileData("SOME_OTHER_KEY=\"x\"\n"));

        // Act
        await EnvironmentOverridesLoader.Apply(_fileSystem, OverrideFilePath);

        // Assert
        Environment.GetEnvironmentVariable(_key).Should().Be("inherited-value");
    }

    [Fact]
    public async Task Should_do_nothing_when_file_does_not_exist()
    {
        // Arrange
        Environment.SetEnvironmentVariable(_key, "inherited-value");

        // Act
        var act = async () => await EnvironmentOverridesLoader.Apply(_fileSystem, OverrideFilePath);

        // Assert
        await act.Should().NotThrowAsync();
        Environment.GetEnvironmentVariable(_key).Should().Be("inherited-value");
    }

    [Fact]
    public async Task Should_do_nothing_when_no_file_is_configured()
    {
        // Arrange — an instance whose deployment sets no override file path runs on the inherited
        // environment; there is no location to fall back to.
        Environment.SetEnvironmentVariable(_key, "inherited-value");

        // Act
        var failure = await EnvironmentOverridesLoader.Apply(_fileSystem, path: null);

        // Assert
        failure.Should().BeNull();
        Environment.GetEnvironmentVariable(_key).Should().Be("inherited-value");
    }

    [Fact]
    public async Task Should_not_throw_when_the_file_cannot_be_parsed()
    {
        // Arrange — a line the format does not accept must not stop startup. This runs before the
        // host builder, so a throw here escapes ahead of the fallback host, and Restart=always
        // turns it into an endless restart loop.
        _fileSystem.AddFile(OverrideFilePath, new MockFileData("BARE_KEY_WITHOUT_EQUALS\n"));

        // Act
        var act = async () => await EnvironmentOverridesLoader.Apply(_fileSystem, OverrideFilePath);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Should_apply_nothing_when_the_file_cannot_be_parsed()
    {
        // Arrange — the file is applied as a whole or not at all: applying entry by entry would
        // leave the process with the first entry set and everything after the syntax error missing.
        Environment.SetEnvironmentVariable(_key, "inherited-value");
        _fileSystem.AddFile(OverrideFilePath,
            new MockFileData($"{_key}=\"override-value\"\nBARE_KEY_WITHOUT_EQUALS\n"));

        // Act
        await EnvironmentOverridesLoader.Apply(_fileSystem, OverrideFilePath);

        // Assert
        Environment.GetEnvironmentVariable(_key).Should().Be("inherited-value");
    }

    [Fact]
    public async Task Should_report_the_failure_when_the_file_cannot_be_parsed()
    {
        // Arrange — swallowing the failure silently would trade a restart loop for an instance
        // running on a configuration nobody can see, so the caller has to be able to log it.
        _fileSystem.AddFile(OverrideFilePath, new MockFileData("BARE_KEY_WITHOUT_EQUALS\n"));

        // Act
        var failure = await EnvironmentOverridesLoader.Apply(_fileSystem, OverrideFilePath);

        // Assert
        failure.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_report_the_failure_when_the_file_cannot_be_read()
    {
        // Arrange — models the file left root-owned and unreadable by a shell edit.
        var fileSystem = Substitute.For<IFileSystem>();
        fileSystem.File.Exists(OverrideFilePath).Returns(true);
        fileSystem.File.ReadAllTextAsync(OverrideFilePath, Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedAccessException());

        // Act
        var failure = await EnvironmentOverridesLoader.Apply(fileSystem, OverrideFilePath);

        // Assert
        failure.Should().BeOfType<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Should_report_no_failure_when_the_overrides_are_applied()
    {
        // Arrange
        _fileSystem.AddFile(OverrideFilePath, new MockFileData($"{_key}=\"override-value\"\n"));

        // Act
        var failure = await EnvironmentOverridesLoader.Apply(_fileSystem, OverrideFilePath);

        // Assert
        failure.Should().BeNull();
    }

    [Fact]
    public async Task Should_report_no_failure_when_the_file_does_not_exist()
    {
        // Arrange

        // Act
        var failure = await EnvironmentOverridesLoader.Apply(_fileSystem, OverrideFilePath);

        // Assert
        failure.Should().BeNull();
    }
}
