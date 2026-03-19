using System.Text.RegularExpressions;

namespace Blazor.DevAssets;

public static partial class WebAssetContentRegex
{
    /// <summary>
    /// https://regex101.com/r/iFwIAe/1
    /// </summary>
    [GeneratedRegex(@"^([A-Z]+\:)(\\+[A-Za-z0-9\-]+)+\\+(\.nuget|src)\\+(.*)", RegexOptions.CultureInvariant)]
    private static partial Regex Matcher();

    public static bool IsMatch(string requestPath) => Matcher().IsMatch(requestPath);

    public static MatchCollection Matches(string requestPath) => Matcher().Matches(requestPath);
}
