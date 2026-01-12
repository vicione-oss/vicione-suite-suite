using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Core.Module.Utils;

public static partial class ModuleNameVersionRegex
{
    [GeneratedRegex(@"(?'version'\d+\.\d+\.\d+)-(?'civersion'ci[0-9]+)?", RegexOptions.CultureInvariant)]

    private static partial Regex Matcher();

    /// <summary>
    /// https://regex101.com/r/muwDlB/1
    /// </summary>
    public static bool GetVersions(string version, [NotNullWhen(true)] out string? parsed, out string? ciVersion)
    {
        parsed = null;
        ciVersion = null;

        var match = Matcher().Match(version);
        if (!match.Success)
            return false;

        parsed = match.Groups["version"].Value;
        ciVersion = match.Groups["civersion"].Success ? match.Groups["civersion"].Value : null;
        return true;
    }
}
