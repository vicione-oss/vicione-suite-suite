using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Core.OS.Tests.Security;

/// <summary>
/// The Content Security Policy source expression a browser computes for the inline stylesheet of a
/// rendered page, so a test can check that the header allows the block the page really sent.
/// Comparing the header against the stylesheet constant instead would pass however far the rendered
/// block and the hashed one have drifted apart.
/// </summary>
internal static partial class InlineStyleSheet
{
    public static string HashOf(string html)
    {
        var match = StyleBlock().Match(html);
        match.Success.Should().BeTrue("the page carries an inline stylesheet");

        var css = Encoding.UTF8.GetBytes(match.Groups["css"].Value);

        return $"'sha256-{Convert.ToBase64String(SHA256.HashData(css))}'";
    }

    [GeneratedRegex("<style>(?<css>.*?)</style>", RegexOptions.Singleline)]
    private static partial Regex StyleBlock();
}
