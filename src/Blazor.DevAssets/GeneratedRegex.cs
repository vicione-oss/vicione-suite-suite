using System.Text.RegularExpressions;

namespace Blazor.DevAssets;

public static partial class WebAssetContentNuGetRegex
{
    [GeneratedRegex(@"^.*_*content.([A-Za-z]+\.)+[A-Za-z]+.", RegexOptions.CultureInvariant)]
    private static partial Regex Matcher();
    public static Match Match(string requestPath) => Matcher().Match(requestPath);
}

public static partial class WebAssetDotnetRegex
{
    [GeneratedRegex(@"dotnet([0-9\.\-rc]+)\.js", RegexOptions.CultureInvariant)]
    private static partial Regex Matcher();
    public static bool IsMatch(string requestPath) => Matcher().IsMatch(requestPath);

    public static string Replace(string text) => Matcher().Replace(text,
        delegate (Match match)
        {
            return text.Replace(match.Groups[1].Value, "", StringComparison.Ordinal);
        });
}

public static partial class WebAssetContentRegex
{
    [GeneratedRegex(@"^([A-Z]+\:)(\\+[A-Za-z0-9\-]+)+\\+(\.nuget|src)\\+(.*)", RegexOptions.CultureInvariant)]
    private static partial Regex Matcher();
    public static bool IsMatch(string requestPath) => Matcher().IsMatch(requestPath);

    public static MatchCollection Matches(string requestPath) => Matcher().Matches(requestPath);
}
