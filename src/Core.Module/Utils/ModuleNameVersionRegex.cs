using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Semver;

namespace Core.Module.Utils;

public static partial class ModuleNameVersionRegex
{
    /// <summary>
    /// https://regex101.com/r/muwDlB/2
    /// </summary>
    [GeneratedRegex(@"(?'version'\d+\.\d+\.\d+)-(?'civersion'[ci|rc]+[0-9]+)?", RegexOptions.CultureInvariant)]

    private static partial Regex Matcher();

    public static bool GetVersion(string moduleName, [NotNullWhen(true)] out SemVersion? parsed)
    {
        parsed = null;

        // We expect names like:
        // 1.24.3-win-x64_0.19.0.json
        // 0.35.0-ci1733049-win-x64.zip
        // 0.35.1-rc1-win-x64.zip
        var match = Matcher().Match(moduleName);
        if (!match.Success)
            return false;

        // The ci part is optional so we have to check if it exists before we can parse the version.
        if (!match.Groups["civersion"].Success)
        {
            return SemVersion.TryParse(match.Groups["version"].Value, out parsed);
        }

        return SemVersion.TryParse($"{match.Groups["version"].Value}-{match.Groups["civersion"].Value}", out parsed);
    }
}
