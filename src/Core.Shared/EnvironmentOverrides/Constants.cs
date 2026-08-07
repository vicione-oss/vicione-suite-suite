namespace Core.Shared.EnvironmentOverrides;

public static class Constants
{
    /// <summary>
    /// Environment variable names that may be written to the override file. Keys are written to
    /// the file verbatim, so a name containing a newline, quote or '=' could append further lines
    /// and set variables that were never submitted; only names that cannot alter the file
    /// structure are accepted. Covers the <c>OTEL_*</c> and .NET <c>Section__Key</c> forms.
    /// </summary>
    /// <remarks>
    /// Anchored with <c>\A</c>/<c>\z</c> rather than <c>^</c>/<c>$</c> on purpose: <c>$</c> also
    /// matches before a trailing newline, which would accept a key that appends a line.
    /// </remarks>
    public const string KeyPattern = @"\A[A-Za-z_][A-Za-z0-9_]*\z";
}
