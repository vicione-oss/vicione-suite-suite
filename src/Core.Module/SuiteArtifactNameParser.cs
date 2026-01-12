using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Core.Module;

internal static partial class SuiteArtifactNameParser
{
    //https://regex101.com/r/T04NnL/5
    [GeneratedRegex(@"^vicione-suite_(?<version>\d+\.\d+\.\d+(\~\d+)*)_(?<arch>(arm64|amd64)+)\.deb(.minisig)*$", RegexOptions.CultureInvariant)]
    private static partial Regex NameMatcher();

    //https://regex101.com/r/t7vwz8/1
    [GeneratedRegex(@"^vicione-suite_(?<version>\d+\.\d+\.\d+(\~\d+)*)_(?<arch>(arm64|amd64)+)_(?'hmversion'\d+\.\d+\.\d+)\.json$", RegexOptions.CultureInvariant)]
    private static partial Regex MetaMatcher();

    public static bool TryParse(string packageName,
        [NotNullWhen(true)] out string? suiteVersion,
        [NotNullWhen(true)] out string? architecture)
    {
        suiteVersion = architecture = null;

        // vicione-suite_1.0.4[~24343243]_arm64.deb[.minisig]
        var match = NameMatcher().Match(packageName);

        // Actually we only support arm64 and amd64 so other architectures don't get accepted
        if (!match.Success)
            return false;

        // vicione-suite_1.0.4~1234_arm64.deb -> vicione-suite_1.0.4-ci1234_arm64.deb
        suiteVersion = JFrogCiToNet(match.Groups["version"].Value);
        architecture = match.Groups["arch"].Value;

        return !string.IsNullOrEmpty(suiteVersion)
            && !string.IsNullOrEmpty(architecture);
    }

    public static bool TryParseMetadata(string packageName,
        [NotNullWhen(true)] out string? suiteVersion,
        [NotNullWhen(true)] out string? architecture,
        [NotNullWhen(true)] out Version? hostManagementVersion)
    {
        suiteVersion = architecture = null;
        hostManagementVersion = null;

        // vicione-suite_1.0.4[~24343243]_arm64_1.1.1.json
        var match = MetaMatcher().Match(packageName);

        // Actually we only support arm64 and amd64 so other architectures don't get accepted
        if (!match.Success)
            return false;

        // vicione-suite_1.0.4~1234_arm64_1.1.1.json -> vicione-suite_1.0.4-ci1234_arm64_1.1.1.json
        suiteVersion = JFrogCiToNet(match.Groups["version"].Value);
        architecture = match.Groups["arch"].Value;
        hostManagementVersion = Version.Parse(match.Groups["hmversion"].Value);

        return !string.IsNullOrEmpty(suiteVersion)
            && !string.IsNullOrEmpty(architecture)
            && hostManagementVersion != null;
    }

    private static string JFrogCiToNet(string str) => str.Replace("~", "-ci", StringComparison.Ordinal);
}
