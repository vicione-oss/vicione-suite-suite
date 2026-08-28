using Core.OS.EnvironmentOverrides;

namespace Core.OS.Tests.EnvironmentOverrides;

public sealed class EnvironmentOverridesFormatTests
{
    [Fact]
    public void Should_write_one_quoted_entry_per_line()
    {
        // Arrange
        var overrides = new Dictionary<string, string> { ["FIRST"] = "one", ["SECOND"] = "two" };

        // Act
        var contents = EnvironmentOverridesFormat.Serialize(overrides);

        // Assert
        contents.Should().Be("FIRST=\"one\"\nSECOND=\"two\"\n");
    }

    [Fact]
    public void Should_parse_back_what_was_written()
    {
        // Arrange
        var overrides = new Dictionary<string, string> { ["FIRST"] = "one", ["SECOND"] = "two" };

        // Act
        var result = EnvironmentOverridesFormat.Parse(EnvironmentOverridesFormat.Serialize(overrides));

        // Assert
        result.Should().Equal(overrides);
    }

    [Fact]
    public void Should_write_the_entries_ordered_by_name()
    {
        // Arrange — names are grouped in the file so an operator can find one, and the same set of
        // overrides has to produce the same bytes however the caller's dictionary enumerates.
        var overrides = new Dictionary<string, string> { ["ZULU"] = "last", ["ALPHA"] = "first" };

        // Act
        var contents = EnvironmentOverridesFormat.Serialize(overrides);

        // Assert
        contents.Should().Be("ALPHA=\"first\"\nZULU=\"last\"\n");
    }

    [Fact]
    public void Should_parse_an_empty_file_as_no_overrides()
    {
        // Act
        var result = EnvironmentOverridesFormat.Parse(string.Empty);

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("KEY=\"value\"\n")]
    // Every written line ends in '\n', so the final split element is always empty.
    [InlineData("KEY=\"value\"")]
    // A checkout or an editor can rewrite the line endings without touching a single value.
    [InlineData("KEY=\"value\"\r\n")]
    public void Should_accept_line_ending(string contents)
    {
        // Act
        var result = EnvironmentOverridesFormat.Parse(contents);

        // Assert
        result.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, string>("KEY", "value"));
    }

    [Theory]
    [InlineData("BARE_KEY_WITHOUT_EQUALS\n", "a line carrying no '='")]
    [InlineData("KEY=value\n", "an unquoted value")]
    [InlineData("KEY='value'\n", "a single-quoted value")]
    [InlineData("KEY=\"unterminated\n", "a value missing its closing quote")]
    [InlineData("KEY=\"say \"hi\"\"\n", "a value with an unescaped quote")]
    [InlineData("KEY=\"trailing\\\"\n", "a value ending in a dangling backslash")]
    [InlineData("KEY=\"\\q\"\n", "an escape the writer never emits")]
    [InlineData("KEY=\"\\xZZ\"\n", "a hex escape without hex digits")]
    [InlineData("KEY=\"\\x7\"\n", "a hex escape with a single digit")]
    [InlineData("# comment\n", "a comment")]
    [InlineData("export KEY=\"value\"\n", "an 'export' prefix")]
    [InlineData("KEY = \"value\"\n", "whitespace around '='")]
    [InlineData("IN VALID=\"value\"\n", "a name the suite would refuse to write")]
    // Serialize writes every name once, so a repeated name is not a last-one-wins instruction.
    [InlineData("KEY=\"one\"\nKEY=\"two\"\n", "a name set more than once")]
    public void Should_reject(string contents, string because)
    {
        // Arrange — the suite is the only writer, so anything it would not have produced is a
        // damaged file. Interpreting it would apply an override nobody stored; rejecting the file
        // as a whole leaves the instance on the inherited environment until the file is deleted.

        // Act
        var act = () => EnvironmentOverridesFormat.Parse(contents);

        // Assert
        act.Should().Throw<FormatException>(because);
    }

    [Fact]
    public void Should_name_the_offending_line()
    {
        // Arrange — the message is the only thing an operator gets before deleting the file.
        var contents = "FIRST=\"one\"\nSECOND=\"two\"\nBROKEN\n";

        // Act
        var act = () => EnvironmentOverridesFormat.Parse(contents);

        // Assert
        act.Should().Throw<FormatException>().WithMessage("*line 3*");
    }

    [Fact]
    public void Should_not_echo_the_value_in_the_error()
    {
        // Arrange — values are where secrets land, so a malformed line must not reach the log.
        var contents = "KEY=\"Host=db;Password=hunter2\"\\q\n";

        // Act
        var act = () => EnvironmentOverridesFormat.Parse(contents);

        // Assert
        act.Should().Throw<FormatException>().Which.Message.Should().NotContain("hunter2");
    }
}
