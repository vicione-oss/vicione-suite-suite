using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Core.OS.EnvironmentOverrides;
using Core.OS.Instance;
using Core.OS.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;
using Sdk.Instance;

namespace Core.OS.Tests.EnvironmentOverrides;

[Collection(EnvironmentOverridesCollectionDefinition.Name)]
public sealed class EnvironmentOverridesRepositoryTests : IDisposable
{
    private const string HomeDirectory = "/data";
    private const string OverrideFilePath = "/data/env-overrides.env";

    private readonly string? _previousEnv =
        Environment.GetEnvironmentVariable(EnvironmentOverridesFile.EnabledEnvironmentVariable);

    private readonly MockFileSystem _fileSystem = new();

    public EnvironmentOverridesRepositoryTests()
        => Environment.SetEnvironmentVariable(EnvironmentOverridesFile.EnabledEnvironmentVariable, "true");

    public void Dispose()
        => Environment.SetEnvironmentVariable(EnvironmentOverridesFile.EnabledEnvironmentVariable, _previousEnv);

    [Fact]
    public async Task Should_round_trip_overrides()
    {
        // Arrange
        var repository = CreateRepository();
        IReadOnlyList<KeyValuePair<string, string>> overrides =
        [
            new("OTEL_EXPORTER_OTLP_ENDPOINT", "http://collector:4317"),
            new("SIMPLE_KEY", "simple value"),
        ];

        // Act
        await repository.Store(overrides, TestContext.Current.CancellationToken);
        var result = await repository.Get(TestContext.Current.CancellationToken);

        // Assert
        result.Should().Equal(overrides);
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("with spaces")]
    [InlineData("with=equals")]
    [InlineData("with\"double-quote")]
    [InlineData("with'apostrophe")]
    [InlineData("with#hash")]
    [InlineData("with$dollar and ${INTERPOLATION}")]
    [InlineData("with\\backslash")]
    [InlineData("with\ttab")]
    [InlineData("line1\nline2")]
    [InlineData("line1\r\nline2")]
    [InlineData("")]
    public async Task Should_round_trip_value(string value)
    {
        // Arrange
        var repository = CreateRepository();
        IReadOnlyList<KeyValuePair<string, string>> overrides = [new("KEY", value)];

        // Act
        await repository.Store(overrides, TestContext.Current.CancellationToken);
        var result = await repository.Get(TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.Value.Should().Be(value);
    }

    [Theory]
    [MemberData(nameof(GetControlCharacterCodes))]
    public async Task Should_round_trip_values_containing_a_control_character(int characterCode)
    {
        // Arrange — a value pasted from a terminal, a log line or a password manager can carry any
        // control character. Written unescaped they produce a file the loader cannot parse, which
        // discards every override on the next start, not just the offending one.
        var repository = CreateRepository();
        var value = "before" + (char)characterCode + "after";
        IReadOnlyList<KeyValuePair<string, string>> overrides = [new("KEY", value)];

        // Act
        await repository.Store(overrides, TestContext.Current.CancellationToken);
        var result = await repository.Get(TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.Value.Should().Be(value);
    }

    // Every C0 character except NUL, which cannot be stored at all — see
    // Should_reject_value_containing_nul.
    public static TheoryData<int> GetControlCharacterCodes() => new(Enumerable.Range(1, 0x1F).Append(0x7F));

    [Theory]
    [InlineData('5')]
    [InlineData('A')]
    [InlineData('f')]
    [InlineData('0')]
    public async Task Should_round_trip_a_control_character_followed_by_a_hex_digit(char following)
    {
        // Arrange — a hex escape that greedily swallowed the following character would silently
        // store something other than what was submitted.
        var repository = CreateRepository();
        var value = "before\u0008" + following + "after";
        IReadOnlyList<KeyValuePair<string, string>> overrides = [new("KEY", value)];

        // Act
        await repository.Store(overrides, TestContext.Current.CancellationToken);
        var result = await repository.Get(TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.Value.Should().Be(value);
    }

    [Fact]
    public async Task Should_write_a_file_without_raw_control_characters()
    {
        // Arrange — an operator inspects this file before deleting it, so no value may put a raw
        // tab, newline or escape character inside a line where nobody can see it.
        var repository = CreateRepository();
        IReadOnlyList<KeyValuePair<string, string>> overrides =
            [new("KEY", "tab\there\nnewline\u001bescape")];

        // Act
        await repository.Store(overrides, TestContext.Current.CancellationToken);

        // Assert
        var lines = await _fileSystem.File.ReadAllLinesAsync(OverrideFilePath,
            TestContext.Current.CancellationToken);
        lines.Should().OnlyContain(line => !line.Any(char.IsControl));
    }

    [Fact]
    public async Task Should_reject_value_containing_nul()
    {
        // Arrange — Environment.SetEnvironmentVariable truncates at NUL without complaining, so a
        // stored override would apply a different value than the one submitted.
        var repository = CreateRepository();
        IReadOnlyList<KeyValuePair<string, string>> overrides = [new("KEY", "before\0after")];

        // Act
        var act = () => repository.Store(overrides, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Should_not_echo_the_rejected_value_in_the_error()
    {
        // Arrange — values are where secrets land, which is why the stored-overrides log line
        // records keys only; a refusal must not undo that.
        var repository = CreateRepository();
        IReadOnlyList<KeyValuePair<string, string>> overrides = [new("KEY", "s3cr3t-p4ssw0rd\0")];

        // Act
        var act = () => repository.Store(overrides, TestContext.Current.CancellationToken);

        // Assert
        var exception = await act.Should().ThrowAsync<ArgumentException>();
        exception.Which.Message.Should().Contain("KEY").And.NotContain("s3cr3t-p4ssw0rd");
    }

    [Fact]
    public async Task Should_leave_stored_overrides_untouched_when_a_value_is_rejected()
    {
        // Arrange — as for keys, the caller submits a complete desired state, so a batch with one
        // unstorable value must not be written in part.
        var repository = CreateRepository();
        IReadOnlyList<KeyValuePair<string, string>> stored = [new("VALID_KEY", "kept")];
        await repository.Store(stored, TestContext.Current.CancellationToken);

        IReadOnlyList<KeyValuePair<string, string>> batchWithInvalidValue =
            [new("ALSO_VALID", "new"), new("WITH_NUL", "bad\0value")];

        // Act
        var act = () => repository.Store(batchWithInvalidValue, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
        var result = await repository.Get(TestContext.Current.CancellationToken);
        result.Should().Equal(stored);
    }

    [Fact]
    public async Task Should_return_empty_when_file_does_not_exist()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var result = await repository.Get(TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_refuse_to_read_when_the_feature_is_switched_off()
    {
        // Arrange — reporting "no overrides" instead would make an instance without the feature
        // look like one with an empty file, and the save that follows would never be applied.
        Environment.SetEnvironmentVariable(EnvironmentOverridesFile.EnabledEnvironmentVariable, null);
        var repository = CreateRepository();

        // Act
        var act = () => repository.Get(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Should_refuse_to_store_when_the_feature_is_switched_off()
    {
        // Arrange — a file nothing reads at startup is worse than a refused save: the caller has to
        // learn that what it stored will never take effect.
        Environment.SetEnvironmentVariable(EnvironmentOverridesFile.EnabledEnvironmentVariable, null);
        var repository = CreateRepository();
        IReadOnlyList<KeyValuePair<string, string>> overrides = [new("VALID_KEY", "value")];

        // Act
        var act = () => repository.Store(overrides, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Should_propagate_the_failure_when_the_file_cannot_be_read()
    {
        // Arrange — models the file left root-owned by a shell edit. Reporting it as "no overrides"
        // would make an unreadable file indistinguishable from an empty one: a save-what-you-see
        // panel renders an empty grid, and the next save truncates the file to what the admin typed.
        var fileSystem = Substitute.For<IFileSystem>();
        fileSystem.Path.Returns(_fileSystem.Path);
        fileSystem.File.Exists(OverrideFilePath).Returns(true);
        fileSystem.File.ReadAllTextAsync(OverrideFilePath, Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedAccessException());
        var repository = CreateRepository(fileSystem);

        // Act
        var act = () => repository.Get(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Should_propagate_the_failure_when_the_file_cannot_be_parsed()
    {
        // Arrange — the file may still hold every override, so reporting none of them is the one
        // answer that loses them: a save-what-you-see panel would truncate the file on next save.
        var repository = CreateRepository();
        _fileSystem.AddFile(OverrideFilePath, new MockFileData("BARE_KEY_WITHOUT_EQUALS\n"));

        // Act
        var act = () => repository.Get(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task Should_reject_key_smuggling_an_additional_variable()
    {
        // Arrange — a key that closes its own quote and opens another line writes a file that
        // parses cleanly but sets a second variable never present in the submitted overrides.
        var repository = CreateRepository();
        IReadOnlyList<KeyValuePair<string, string>> smuggled =
            [new("FOO=\"benign\"\nConnectionStrings__Default", "Host=attacker;Password=p")];

        // Act
        var act = () => repository.Store(smuggled, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("FOO=\"benign\"\nSMUGGLED")]
    [InlineData("WITH\nNEWLINE")]
    [InlineData("WITH SPACE")]
    [InlineData("WITH=EQUALS")]
    [InlineData("WITH\"QUOTE")]
    [InlineData("WITH#HASH")]
    [InlineData("WITH$DOLLAR")]
    [InlineData("WITH-DASH")]
    [InlineData("WITH.DOT")]
    [InlineData("1LEADING_DIGIT")]
    [InlineData(" LEADING_SPACE")]
    [InlineData("TRAILING_SPACE ")]
    // An otherwise valid key with a single trailing newline: '$' matches before a final newline,
    // so a "^...$" pattern would let this through and write a file that no longer parses.
    [InlineData("TRAILING_NEWLINE\n")]
    [InlineData("TRAILING_CRLF\r\n")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_reject_invalid_key(string key)
    {
        // Arrange
        var repository = CreateRepository();
        IReadOnlyList<KeyValuePair<string, string>> overrides = [new(key, "value")];

        // Act
        var act = () => repository.Store(overrides, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Should_leave_stored_overrides_untouched_when_a_key_is_rejected()
    {
        // Arrange — a rejected batch must not be written partially: the previously stored
        // overrides stay authoritative.
        var repository = CreateRepository();
        IReadOnlyList<KeyValuePair<string, string>> stored = [new("VALID_KEY", "kept")];
        await repository.Store(stored, TestContext.Current.CancellationToken);

        IReadOnlyList<KeyValuePair<string, string>> batchWithInvalidKey =
            [new("ALSO_VALID", "new"), new("IN VALID", "new")];

        // Act
        var act = () => repository.Store(batchWithInvalidKey, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
        var result = await repository.Get(TestContext.Current.CancellationToken);
        result.Should().Equal(stored);
    }

    [Theory]
    [InlineData("SIMPLE")]
    [InlineData("lower_case")]
    [InlineData("_LEADING_UNDERSCORE")]
    [InlineData("WITH_DIGITS_123")]
    [InlineData("Instance__HomeDirectory")]
    [InlineData("OTEL_EXPORTER_OTLP_ENDPOINT")]
    public async Task Should_accept_valid_key(string key)
    {
        // Arrange
        var repository = CreateRepository();
        IReadOnlyList<KeyValuePair<string, string>> overrides = [new(key, "value")];

        // Act
        await repository.Store(overrides, TestContext.Current.CancellationToken);

        // Assert
        var result = await repository.Get(TestContext.Current.CancellationToken);
        result.Should().Equal(overrides);
    }

    private EnvironmentOverridesRepository CreateRepository(IFileSystem? fileSystem = null)
    {
        var effectiveFileSystem = fileSystem ?? _fileSystem;
        var atomicWriter = new AtomicFileWriter(effectiveFileSystem);
        var instanceOptions = Options.Create(new InstanceOptions
        {
            HomeDirectory = HomeDirectory,
            CacheDirectory = "/cache",
            BackupDirectory = "/backup",
            Type = InstanceType.Standalone,
        });
        var logger = Substitute.For<ILogger<EnvironmentOverridesRepository>>();

        return new EnvironmentOverridesRepository(effectiveFileSystem, atomicWriter, instanceOptions, logger);
    }
}
