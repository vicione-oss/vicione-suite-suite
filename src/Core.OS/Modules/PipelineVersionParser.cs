using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Core.OS.Modules;

internal static partial class PipelineVersionParser
{
    private const string VersionGroupName = "version";
    private const string CommitIdGroupName = "civersion";

    [GeneratedRegex(@$"^v?(?'{VersionGroupName}'[0-9]+\.[0-9]+\.[0-9]+)(\s\((?'{CommitIdGroupName}'[0-9a-z]+)\))?$", RegexOptions.CultureInvariant)]

    private static partial Regex Matcher();

    /// <summary>
    /// https://regex101.com/r/KvLVXq/1
    /// </summary>
    /// <param name="version">0.38.1 (40454a3e)</param>
    /// <param name="parsed">0.38.1</param>
    /// <param name="commitId">40454a3e</param>
    /// <returns></returns>
    public static bool TryParse(string? version, [NotNullWhen(true)] out string? parsed, out string? commitId)
    {
        parsed = null;
        commitId = null;

        if (string.IsNullOrEmpty(version))
            return false;

        var match = Matcher().Match(version);
        if (!match.Success)
            return false;

        parsed = match.Groups[VersionGroupName].Value;
        commitId = match.Groups[CommitIdGroupName].Success ? match.Groups[CommitIdGroupName].Value : null;
        return true;
    }
}
