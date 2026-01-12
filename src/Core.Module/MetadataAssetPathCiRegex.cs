using System.Text.RegularExpressions;

namespace Core.Module;

internal static partial class MetadataAssetPathCiRegex
{
    [GeneratedRegex(@".*\/(?:[0-9]+\.[0-9]+\.[0-9]+-)?ci\-?[0-9]+-[win|linux]+-.*\.json", RegexOptions.CultureInvariant)]

    private static partial Regex Matcher();

    /// <summary>
    /// https://regex101.com/r/V4nuup/1
    /// </summary>
    public static bool IsMatch(string requestPath) => Matcher().IsMatch(requestPath);
}
