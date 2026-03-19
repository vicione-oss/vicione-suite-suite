using System.Text.RegularExpressions;

namespace Blazor.DevAssets;

public static partial class WebAssetContentNuGetRegex
{
    /// <summary>
    /// https://regex101.com/r/zct1ZF/1
    /// </summary>
    [GeneratedRegex(@"^.*_*content.([A-Za-z]+\.)+[A-Za-z]+.", RegexOptions.CultureInvariant)]
    private static partial Regex Matcher();

    public static Match Match(string requestPath) => Matcher().Match(requestPath);
}
