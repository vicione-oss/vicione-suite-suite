using System.Text.RegularExpressions;

namespace Blazor.DevAssets;

public static partial class WebAssetDotnetRegex
{
    /// <summary>
    /// https://regex101.com/r/1bc782/1
    /// </summary>
    [GeneratedRegex(@"dotnet([0-9\.\-rc]+)\.js", RegexOptions.CultureInvariant)]
    private static partial Regex Matcher();

    public static bool IsMatch(string requestPath) => Matcher().IsMatch(requestPath);

    public static string Replace(string text) => Matcher().Replace(text,
        delegate (Match match)
        {
            return text.Replace(match.Groups[1].Value, "", StringComparison.Ordinal);
        });
}
