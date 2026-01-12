using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Core.Module;

public static partial class MetadataAssetVersionRegex
{
    private const string VersionGroupName = "version";
    private const string CiVersionGroupName = "civersion";

    [GeneratedRegex(@$"^(?'{VersionGroupName}'[0-9]+\.[0-9]+\.[0-9]+)(-(?'{CiVersionGroupName}'ci[0-9]+))?$", RegexOptions.CultureInvariant)]

    private static partial Regex Matcher();

    /// <summary>
    /// https://regex101.com/r/Xtq70Q/1
    /// </summary>
    /// <param name="version">e.g. 12.1.3-ci1399909</param>
    /// <param name="parsed">e.g. 12.1.3</param>
    /// <param name="ciVersion">e.g. ci1399909</param>
    /// <returns></returns>
    public static bool GetVersions(string? version, [NotNullWhen(true)] out string? parsed, out string? ciVersion)
    {
        parsed = null;
        ciVersion = null;

        if (string.IsNullOrEmpty(version))
            return false;

        var match = Matcher().Match(version);
        if (!match.Success)
            return false;

        parsed = match.Groups[VersionGroupName].Value;
        ciVersion = match.Groups[CiVersionGroupName].Success ? match.Groups[CiVersionGroupName].Value : null;
        return true;
    }
}
