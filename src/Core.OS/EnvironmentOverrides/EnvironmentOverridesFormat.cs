using System.Globalization;
using System.Text;

namespace Core.OS.EnvironmentOverrides;

/// <summary>
/// The on-disk format of the override file: one <c>NAME="value"</c> entry per line.
/// Reading and writing live together because they are one contract — <see cref="Parse"/> is
/// only safe to keep this strict as long as it mirrors exactly what <see cref="Serialize"/>
/// emits, and a change to either side that is not made to the other is a round-trip bug.
/// </summary>
internal static class EnvironmentOverridesFormat
{
    /// <summary>
    /// Writes the entries ordered by name so related variables end up grouped in the file and the
    /// same set of overrides always produces the same bytes, independent of how the caller's
    /// dictionary happens to enumerate.
    /// </summary>
    public static string Serialize(IReadOnlyDictionary<string, string> overrides)
    {
        var builder = new StringBuilder();
        foreach (var (key, value) in overrides.OrderBy(o => o.Key, StringComparer.Ordinal))
        {
            builder.Append(key).Append('=').Append('"');
            AppendEscaped(builder, value);
            builder.Append('"').Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    /// Reads back what <see cref="Serialize"/> wrote. Rejects anything else instead of
    /// interpreting it: the suite is the only writer, so a line that does not match is a damaged
    /// file rather than a dialect to accommodate, and guessing at it would apply an override
    /// nobody stored.
    /// </summary>
    /// <returns>All entries, fully materialized so a syntax error surfaces before the caller
    /// applies any of them.</returns>
    /// <exception cref="FormatException">The file is not in the format the suite writes.</exception>
    public static IReadOnlyDictionary<string, string> Parse(string contents)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);

        var lineNumber = 0;
        foreach (var rawLine in contents.Split('\n'))
        {
            lineNumber++;

            var line = rawLine.AsSpan().TrimEnd('\r');
            if (line.IsEmpty)
                continue;

            var (key, value) = ParseEntry(line, lineNumber);

            // A name cannot appear twice: Serialize writes each one once, so a repeated name is a
            // damaged file rather than a last-one-wins instruction. Silently keeping one of the two
            // values would apply a configuration the operator cannot read off the file.
            if (!entries.TryAdd(key, value))
                throw Invalid(lineNumber, $"'{key}' is set more than once.");
        }

        return entries;
    }

    private static KeyValuePair<string, string> ParseEntry(ReadOnlySpan<char> line, int lineNumber)
    {
        var separator = line.IndexOf('=');
        if (separator < 0)
            throw Invalid(lineNumber, "expected NAME=\"value\".");

        var key = line[..separator].ToString();
        if (!EnvironmentOverridesFile.IsValidKey(key))
            throw Invalid(lineNumber, $"'{key}' is not a valid environment variable name.");

        var value = line[(separator + 1)..];
        if (value.Length < 2 || value[0] != '"' || value[^1] != '"')
            throw Invalid(lineNumber, "the value is not enclosed in double quotes.");

        return new(key, UnescapeValue(value[1..^1], lineNumber));
    }

    // Overrides are a literal-value store: Parse must return exactly what Serialize was given,
    // for any value a user types. Double quoting is the only .env encoding that can hold any
    // value (apostrophes, newlines), so the characters that would otherwise end the value or
    // start an escape are escaped. Escaping "$" keeps the file safe to feed to any tool that
    // expands variable references — what the user typed is what gets set, not a re-evaluated
    // expression. Control characters are escaped too: raw they either make the whole file
    // unparseable, which discards every override on the next start, or sit invisibly in a file
    // an operator is expected to be able to read before deleting it.
    private static void AppendEscaped(StringBuilder builder, string value)
    {
        foreach (var character in value)
            AppendEscaped(builder, character);
    }

    private static void AppendEscaped(StringBuilder builder, char character)
    {
        switch (character)
        {
            case '\\':
                builder.Append(@"\\");
                break;
            case '"':
                builder.Append("\\\"");
                break;
            case '$':
                builder.Append("\\$");
                break;
            case '\t':
                builder.Append(@"\t");
                break;
            case '\r':
                builder.Append(@"\r");
                break;
            case '\n':
                builder.Append(@"\n");
                break;
            default:
                // \xHH consumes exactly two digits, so a hex digit that follows stays a literal.
                if (char.IsControl(character))
                    builder.Append(CultureInfo.InvariantCulture, $@"\x{(int)character:X2}");
                else
                    builder.Append(character);

                break;
        }
    }

    private static string UnescapeValue(ReadOnlySpan<char> value, int lineNumber)
    {
        var builder = new StringBuilder(value.Length);

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];

            // Every quote Serialize wrote inside a value is escaped, so a bare one means the
            // value ended earlier than the closing quote this line was split on.
            if (character == '"')
                throw Invalid(lineNumber, "the value contains an unescaped double quote.");

            if (character != '\\')
            {
                builder.Append(character);
                continue;
            }

            index++;
            if (index == value.Length)
                throw Invalid(lineNumber, "the value ends with a dangling backslash.");

            builder.Append(Unescaped(value, ref index, lineNumber));
        }

        return builder.ToString();
    }

    private static char Unescaped(ReadOnlySpan<char> value, ref int index, int lineNumber)
        => value[index] switch
        {
            '\\' => '\\',
            '"' => '"',
            '$' => '$',
            't' => '\t',
            'r' => '\r',
            'n' => '\n',
            'x' => ReadHexEscape(value, ref index, lineNumber),

            // Unknown escapes are refused rather than passed through: Serialize never emits one,
            // so silently dropping the backslash would hand back a value that was never stored.
            _ => throw Invalid(lineNumber, $@"'\{value[index]}' is not a valid escape sequence."),
        };

    private static char ReadHexEscape(ReadOnlySpan<char> value, ref int index, int lineNumber)
    {
        var digits = value.Slice(index + 1, Math.Min(2, value.Length - index - 1));
        if (digits.Length < 2
            || !int.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
            throw Invalid(lineNumber, @"'\x' must be followed by two hexadecimal digits.");

        index += 2;
        return (char)code;
    }

    // Never quotes the line itself: values are where secrets land, which is also why the
    // stored-overrides log line records keys only.
    private static FormatException Invalid(int lineNumber, string reason)
        => new($"The environment overrides file is malformed on line {lineNumber}: {reason}");
}
